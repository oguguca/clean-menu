using System.Runtime.InteropServices;
using CleanMenu.Core.Cleanup;

namespace CleanMenu.Infrastructure.Cleanup;

/// <summary>Recycle Bin through the Shell API (all drives).</summary>
public sealed partial class WindowsRecycleBin : IRecycleBin
{
    private const uint NoConfirmation = 0x1; // Clean Menu shows its own confirmation first.
    private const uint NoProgressUi = 0x2;
    private const uint NoSound = 0x4;
    private const int Ok = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct ShQueryRbInfo
    {
        public int Size;
        public long SizeBytes;
        public long ItemCount;
    }

    public RecycleBinInfo? Query()
    {
        var info = new ShQueryRbInfo { Size = Marshal.SizeOf<ShQueryRbInfo>() };
        return SHQueryRecycleBinW(null, ref info) == Ok ? new RecycleBinInfo(info.ItemCount, info.SizeBytes) : null;
    }

    public bool Empty()
    {
        var result = SHEmptyRecycleBinW(IntPtr.Zero, null, NoConfirmation | NoProgressUi | NoSound);

        // E_UNEXPECTED is returned when the bin is already empty.
        return result is Ok or unchecked((int)0x8000FFFF);
    }

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int SHQueryRecycleBinW(string? rootPath, ref ShQueryRbInfo info);

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int SHEmptyRecycleBinW(IntPtr hwnd, string? rootPath, uint flags);
}
