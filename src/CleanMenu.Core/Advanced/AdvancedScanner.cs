using CleanMenu.Core.Cleanup;

namespace CleanMenu.Core.Advanced;

/// <summary>Computes file content hashes.</summary>
public interface IFileHasher
{
    /// <summary>SHA-256 as hex, or null when the file cannot be read (in use, removed, no permission).</summary>
    Task<string?> Sha256Async(string path, CancellationToken cancellationToken);
}

/// <summary>Files with exactly the same content.</summary>
public sealed record DuplicateGroup(string Hash, long SizeBytes, IReadOnlyList<CleanupFile> Files)
{
    /// <summary>Space freed by keeping only one copy.</summary>
    public long WastedBytes => SizeBytes * (Files.Count - 1);
}

/// <summary>Space used by one folder (all its content).</summary>
public sealed record FolderSize(string Path, long SizeBytes, int FileCount);

/// <summary>
/// Read-only analyses for the Limpeza Avançada page. Nothing here deletes files: results are shown
/// to the user, who decides what to send to the Recycle Bin.
/// </summary>
public sealed class AdvancedScanner(
    ICleanupFileSystem fileSystem,
    IFileHasher hasher,
    IRecentFiles? recentFiles = null,
    TimeProvider? time = null)
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    public const long DefaultLargeFileBytes = 100L * 1024 * 1024;
    public const long DefaultDuplicateMinBytes = 1L * 1024 * 1024;

    /// <summary>Largest files under <paramref name="root"/>, biggest first.</summary>
    public IReadOnlyList<CleanupFile> FindLargeFiles(
        string root,
        IReadOnlyCollection<string> excluded,
        long minBytes = DefaultLargeFileBytes,
        int maxResults = 200,
        CancellationToken cancellationToken = default)
    {
        var inUse = InUseFilter();
        return Enumerate(root, excluded, cancellationToken)
            .Where(f => f.SizeBytes >= minBytes && AdvancedFileRules.CanOfferLargeFile(f.Path) && !inUse(f))
            .OrderByDescending(f => f.SizeBytes)
            .Take(maxResults)
            .ToList();
    }

    /// <summary>
    /// Duplicate files: grouped by size first (cheap), then confirmed with SHA-256 on the candidates
    /// only. Two files are reported as duplicates only when size and SHA-256 are identical.
    /// </summary>
    public async Task<IReadOnlyList<DuplicateGroup>> FindDuplicatesAsync(
        string root,
        IReadOnlyCollection<string> excluded,
        long minBytes = DefaultDuplicateMinBytes,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(new OperationProgress("Listando arquivos", 0));
        // Recently used files are dropped before hashing: reading them to compare would itself look like use.
        var inUse = InUseFilter();
        var candidates = Enumerate(root, excluded, cancellationToken)
            .Where(f => f.SizeBytes >= minBytes && AdvancedFileRules.CanOfferDuplicate(f.Path) && !inUse(f))
            .GroupBy(f => f.SizeBytes)
            .Where(g => g.Count() > 1)
            .ToList();

        var total = candidates.Sum(g => g.Count());
        var processed = 0;
        var groups = new List<DuplicateGroup>();

        foreach (var sameSize in candidates)
        {
            var byHash = new Dictionary<string, List<CleanupFile>>(StringComparer.Ordinal);
            foreach (var file in sameSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new OperationProgress("Comparando o conteúdo dos arquivos", processed++, total));
                var hash = await hasher.Sha256Async(file.Path, cancellationToken).ConfigureAwait(false);
                if (hash is null)
                {
                    continue;
                }

                if (!byHash.TryGetValue(hash, out var list))
                {
                    byHash[hash] = list = [];
                }

                list.Add(file);
            }

            groups.AddRange(byHash
                .Where(kv => kv.Value.Count > 1)
                .Select(kv => new DuplicateGroup(kv.Key, sameSize.Key, kv.Value.OrderBy(f => f.LastWriteUtc).ToList())));
        }

        progress?.Report(new OperationProgress("Comparação concluída", total, total));
        return groups.OrderByDescending(g => g.WastedBytes).ToList();
    }

    /// <summary>Size of each direct subfolder of <paramref name="root"/>, biggest first.</summary>
    public IReadOnlyList<FolderSize> FindLargestFolders(string root, IReadOnlyCollection<string> excluded, CancellationToken cancellationToken = default)
    {
        var prefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        var sizes = new Dictionary<string, (long Bytes, int Count)>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Enumerate(root, excluded, cancellationToken))
        {
            var relative = file.Path[prefix.Length..];
            var separator = relative.IndexOf(Path.DirectorySeparatorChar, StringComparison.Ordinal);
            var key = separator < 0 ? root : prefix + relative[..separator];
            var current = sizes.GetValueOrDefault(key);
            sizes[key] = (current.Bytes + file.SizeBytes, current.Count + 1);
        }

        return sizes
            .Select(kv => new FolderSize(kv.Key, kv.Value.Bytes, kv.Value.Count))
            .OrderByDescending(f => f.SizeBytes)
            .ToList();
    }

    /// <summary>
    /// Final check right before a file found by a scan goes to the Recycle Bin: it must still exist,
    /// be unchanged and show no recent use. The access date comes from the scan, because comparing
    /// duplicates reads the files and Windows then records that reading as an access.
    /// </summary>
    public bool CanStillRecycle(CleanupFile scanned)
    {
        ArgumentNullException.ThrowIfNull(scanned);
        if (!AdvancedFileRules.CanRecycle(scanned.Path))
        {
            return false;
        }

        var current = fileSystem.GetFile(scanned.Path);
        if (current is null || current.LastWriteUtc != scanned.LastWriteUtc || current.SizeBytes != scanned.SizeBytes)
        {
            return false;
        }

        var fresh = current with { LastAccessUtc = scanned.LastAccessUtc };
        return !InUseFilter()(fresh);
    }

    /// <summary>Recognizes files the user is using: recent sign of use, or in Windows' recent items.</summary>
    private Func<CleanupFile, bool> InUseFilter()
    {
        var now = _time.GetUtcNow();
        IReadOnlySet<string> recent;
        try
        {
            recent = recentFiles?.GetRecentlyOpened() ?? new HashSet<string>();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            recent = new HashSet<string>();
        }

        return file => AdvancedFileRules.IsRecentlyUsed(file.LastUsedUtc, now) || recent.Contains(file.Path);
    }

    private IEnumerable<CleanupFile> Enumerate(string root, IReadOnlyCollection<string> excluded, CancellationToken cancellationToken)
    {
        if (!fileSystem.DirectoryExists(root))
        {
            yield break;
        }

        var projectCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in fileSystem.EnumerateFiles(root, "*", recursive: true))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (file.IsReparsePoint
                || excluded.Any(e => file.Path.StartsWith(Path.TrimEndingDirectorySeparator(e) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                || IsInsideProject(Path.GetDirectoryName(file.Path), projectCache))
            {
                continue;
            }

            yield return file;
        }
    }

    /// <summary>Files that mark a folder as a software project (everything inside belongs to it).</summary>
    private static readonly HashSet<string> ProjectMarkerFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "package.json", "pyproject.toml", "requirements.txt", "setup.py", "Pipfile", "Cargo.toml", "go.mod", "pom.xml",
        "build.gradle", "build.gradle.kts", "settings.gradle", "composer.json", "Gemfile", "CMakeLists.txt", "Makefile",
        "pubspec.yaml", "Directory.Build.props", "global.json", "project.godot", "ProjectSettings.asset",
    };

    private static readonly HashSet<string> ProjectMarkerExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".sln", ".slnx", ".csproj", ".vbproj", ".fsproj", ".vcxproj", ".uproject", ".xcodeproj",
    };

    private static readonly string[] ProjectMarkerFolders = [".git", ".svn", ".hg"];

    /// <summary>True when the folder or any parent folder is a software project (cached per scan).</summary>
    private bool IsInsideProject(string? directory, Dictionary<string, bool> cache)
    {
        if (string.IsNullOrEmpty(directory))
        {
            return false;
        }

        if (cache.TryGetValue(directory, out var known))
        {
            return known;
        }

        var isProject = ProjectMarkerFolders.Any(f => fileSystem.DirectoryExists(Path.Combine(directory, f)))
            || fileSystem.EnumerateFiles(directory, "*", recursive: false).Any(f =>
                ProjectMarkerFiles.Contains(Path.GetFileName(f.Path)) || ProjectMarkerExtensions.Contains(Path.GetExtension(f.Path)));
        var result = isProject || IsInsideProject(Path.GetDirectoryName(directory), cache);
        cache[directory] = result;
        return result;
    }
}
