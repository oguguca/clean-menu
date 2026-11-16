using System.Runtime.InteropServices;
using CleanMenu.Core.Advanced;

namespace CleanMenu.Infrastructure.Advanced;

/// <summary>
/// Moves one file to the Recycle Bin through the Windows shell (SHFileOperation with undo).
/// Refuses links, folders, network paths and anything inside protected system folders. If a file
/// cannot go to the Recycle Bin (too large), Windows warns the user before deleting it for good.
/// </summary>
public sealed class ShellRecycler : IRecycler
{
    private const uint FoDelete = 0x0003;
    private const ushort FofSilent = 0x0004;
    private const ushort FofNoConfirmation = 0x0010;
    private const ushort FofAllowUndo = 0x0040;
    private const ushort FofNoErrorUi = 0x0400;
    private const ushort FofWantNukeWarning = 0x4000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileOpStruct
    {
        public IntPtr Hwnd;
        public uint Func;
        public string From;
        public string? To;
        public ushort Flags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool AnyOperationsAborted;
        public IntPtr NameMappings;
        public string? ProgressTitle;
    }

    public RecycleOutcome SendToRecycleBin(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal) || IsInsideProtected(path))
        {
            return RecycleOutcome.Refused;
        }

        FileAttributes attributes;
        try
        {
            attributes = File.GetAttributes(path);
        }
        catch (FileNotFoundException)
        {
            return RecycleOutcome.NotFound;
        }
        catch (DirectoryNotFoundException)
        {
            return RecycleOutcome.NotFound;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return RecycleOutcome.Refused;
        }

        if (attributes.HasFlag(FileAttributes.ReparsePoint) || attributes.HasFlag(FileAttributes.Directory))
        {
            return RecycleOutcome.Refused;
        }

        var operation = new ShFileOpStruct
        {
            Func = FoDelete,
            From = Path.GetFullPath(path) + "\0\0", // the API expects a double-null-terminated list
            Flags = FofAllowUndo | FofNoConfirmation | FofSilent | FofNoErrorUi | FofWantNukeWarning,
        };

        var result = SHFileOperationW(ref operation);
        if (operation.AnyOperationsAborted)
        {
            return RecycleOutcome.Cancelled;
        }

        return result == 0 && !File.Exists(path) ? RecycleOutcome.Recycled : RecycleOutcome.InUse;
    }

    private static bool IsInsideProtected(string path) =>
        AdvancedProtectedFolders.Get().Any(folder =>
            path.StartsWith(Path.TrimEndingDirectorySeparator(folder) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

#pragma warning disable SYSLIB1054 // The struct holds strings; classic DllImport marshalling is required here.
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperationW(ref ShFileOpStruct operation);
#pragma warning restore SYSLIB1054
}
