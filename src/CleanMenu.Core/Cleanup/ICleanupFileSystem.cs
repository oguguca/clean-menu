namespace CleanMenu.Core.Cleanup;

/// <summary>A file found during analysis.</summary>
/// <param name="LastAccessUtc">Last time the file was opened, when Windows records it.</param>
/// <param name="CreationUtc">When the file appeared on this disk (copied, downloaded or created).</param>
public sealed record CleanupFile(
    string Path,
    long SizeBytes,
    DateTimeOffset LastWriteUtc,
    FileAttributes Attributes,
    DateTimeOffset? LastAccessUtc = null,
    DateTimeOffset? CreationUtc = null)
{
    public bool IsReparsePoint => Attributes.HasFlag(FileAttributes.ReparsePoint);

    /// <summary>Most recent sign of use: opened, changed or placed on the disk.</summary>
    public DateTimeOffset LastUsedUtc
    {
        get
        {
            var latest = LastWriteUtc;
            if (LastAccessUtc > latest)
            {
                latest = LastAccessUtc.Value;
            }

            if (CreationUtc > latest)
            {
                latest = CreationUtc.Value;
            }

            return latest;
        }
    }
}

/// <summary>Result of trying to delete one file.</summary>
public enum DeleteOutcome
{
    Deleted,
    NotFound,
    InUse,
    AccessDenied,
    Refused,
}

/// <summary>
/// File system operations used by the cleanup engine. Abstracted so the engine is tested against
/// an in-memory fake and never touches real user files in tests.
/// </summary>
public interface ICleanupFileSystem
{
    bool DirectoryExists(string path);

    /// <summary>
    /// Enumerates files under <paramref name="root"/> without following symbolic links or
    /// junctions and skipping folders the user cannot read.
    /// </summary>
    IEnumerable<CleanupFile> EnumerateFiles(string root, string searchPattern, bool recursive);

    /// <summary>Reads the current size, date and attributes, or null when the file no longer exists.</summary>
    CleanupFile? GetFile(string path);

    DeleteOutcome DeleteFile(string path);

    /// <summary>Removes empty subfolders (never <paramref name="root"/> itself and never links).</summary>
    void DeleteEmptyDirectories(string root);
}
