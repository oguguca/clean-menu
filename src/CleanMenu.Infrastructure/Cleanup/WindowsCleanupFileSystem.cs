using CleanMenu.Core.Cleanup;

namespace CleanMenu.Infrastructure.Cleanup;

/// <summary>
/// Real file system access for cleanup. Enumeration never follows symbolic links or junctions
/// (ReparsePoint is skipped) and ignores folders the user cannot read.
/// </summary>
public sealed class WindowsCleanupFileSystem : ICleanupFileSystem
{
    private const int SharingViolation = unchecked((int)0x80070020);
    private const int LockViolation = unchecked((int)0x80070021);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public IEnumerable<CleanupFile> EnumerateFiles(string root, string searchPattern, bool recursive)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = recursive,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            ReturnSpecialDirectories = false,
            MatchType = MatchType.Win32,
        };

        IEnumerable<FileInfo> files;
        try
        {
            files = new DirectoryInfo(root).EnumerateFiles(searchPattern, options);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            yield break;
        }

        using var enumerator = files.GetEnumerator();
        while (true)
        {
            CleanupFile? item = null;
            try
            {
                if (!enumerator.MoveNext())
                {
                    yield break;
                }

                var file = enumerator.Current;
                item = new CleanupFile(file.FullName, file.Length, new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero), file.Attributes);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // File vanished or became unreadable during enumeration; skip it.
            }

            if (item is not null)
            {
                yield return item;
            }
        }
    }

    public CleanupFile? GetFile(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists
                ? new CleanupFile(info.FullName, info.Length, new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero), info.Attributes)
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public DeleteOutcome DeleteFile(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            if (attributes.HasFlag(FileAttributes.ReparsePoint) || attributes.HasFlag(FileAttributes.Directory))
            {
                return DeleteOutcome.Refused;
            }

            if (attributes.HasFlag(FileAttributes.ReadOnly))
            {
                // Temporary files are sometimes left read-only by installers.
                File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
            }

            File.Delete(path);
            return DeleteOutcome.Deleted;
        }
        catch (FileNotFoundException)
        {
            return DeleteOutcome.NotFound;
        }
        catch (DirectoryNotFoundException)
        {
            return DeleteOutcome.NotFound;
        }
        catch (UnauthorizedAccessException)
        {
            return DeleteOutcome.AccessDenied;
        }
        catch (IOException ex) when (ex.HResult is SharingViolation or LockViolation)
        {
            return DeleteOutcome.InUse;
        }
        catch (IOException)
        {
            return DeleteOutcome.InUse;
        }
    }

    public void DeleteEmptyDirectories(string root)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };

        List<string> directories;
        try
        {
            // Deepest first so parents become empty after their children are removed.
            directories = Directory.EnumerateDirectories(root, "*", options)
                .OrderByDescending(d => d.Length)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var directory in directories)
        {
            try
            {
                var info = new DirectoryInfo(directory);
                if (info.Attributes.HasFlag(FileAttributes.ReparsePoint) || info.EnumerateFileSystemInfos().Any())
                {
                    continue;
                }

                info.Delete(recursive: false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // In use or protected: keep it.
            }
        }
    }
}
