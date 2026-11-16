using CleanMenu.Core.Cleanup;

namespace CleanMenu.Core.Tests.Cleanup;

/// <summary>In-memory file system. Tests never touch real files.</summary>
internal sealed class FakeCleanupFileSystem : ICleanupFileSystem
{
    private readonly Dictionary<string, CleanupFile> _files = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> Directories { get; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> Locked { get; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> Denied { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string> DeleteCalls { get; } = [];

    public List<string> EmptyDirectoryCleanups { get; } = [];

    public IReadOnlyCollection<string> Files => _files.Keys;

    public void Add(string path, long size, DateTimeOffset lastWrite, FileAttributes attributes = FileAttributes.Normal)
    {
        _files[path] = new CleanupFile(path, size, lastWrite, attributes);
        var dir = Path.GetDirectoryName(path);
        while (!string.IsNullOrEmpty(dir))
        {
            Directories.Add(dir);
            dir = Path.GetDirectoryName(dir);
        }
    }

    public bool DirectoryExists(string path) => Directories.Contains(path);

    public IEnumerable<CleanupFile> EnumerateFiles(string root, string searchPattern, bool recursive)
    {
        var prefix = root.TrimEnd('\\') + "\\";
        return _files.Values
            .Where(f => f.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Where(f => recursive || !f.Path[prefix.Length..].Contains('\\', StringComparison.Ordinal))
            .Where(f => searchPattern == "*" || Matches(Path.GetFileName(f.Path), searchPattern))
            .ToList();
    }

    public CleanupFile? GetFile(string path) => _files.TryGetValue(path, out var f) ? f : null;

    public DeleteOutcome DeleteFile(string path)
    {
        DeleteCalls.Add(path);
        if (!_files.ContainsKey(path))
        {
            return DeleteOutcome.NotFound;
        }

        if (Locked.Contains(path))
        {
            return DeleteOutcome.InUse;
        }

        if (Denied.Contains(path))
        {
            return DeleteOutcome.AccessDenied;
        }

        _files.Remove(path);
        return DeleteOutcome.Deleted;
    }

    public void DeleteEmptyDirectories(string root) => EmptyDirectoryCleanups.Add(root);

    public void Touch(string path, DateTimeOffset lastWrite) => _files[path] = _files[path] with { LastWriteUtc = lastWrite };

    private static bool Matches(string name, string pattern)
    {
        // Supports the patterns used by the catalog: "prefix*suffix".
        var star = pattern.IndexOf('*', StringComparison.Ordinal);
        return star < 0
            ? name.Equals(pattern, StringComparison.OrdinalIgnoreCase)
            : name.StartsWith(pattern[..star], StringComparison.OrdinalIgnoreCase)
              && name.EndsWith(pattern[(star + 1)..], StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class FakeRecycleBin(RecycleBinInfo? info) : IRecycleBin
{
    public int EmptyCalls { get; private set; }

    public RecycleBinInfo? Query() => info;

    public bool Empty()
    {
        EmptyCalls++;
        return true;
    }
}

internal sealed class ManualClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
