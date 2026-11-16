using System.IO;
using CleanMenu.App.Services;
using CleanMenu.Core.Cleanup;
using CleanMenu.Core.History;
using CleanMenu.Core.Platform;

namespace CleanMenu.App.Tests;

/// <summary>In-memory file system for view model tests. Nothing real is touched.</summary>
internal sealed class MemoryFileSystem : ICleanupFileSystem
{
    private readonly Dictionary<string, CleanupFile> _files = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> Files => _files.Keys;

    public void Add(string path, long size, DateTimeOffset lastWrite) =>
        _files[path] = new CleanupFile(path, size, lastWrite, FileAttributes.Normal);

    public bool DirectoryExists(string path) => _files.Keys.Any(f => f.StartsWith(path + "\\", StringComparison.OrdinalIgnoreCase));

    public IEnumerable<CleanupFile> EnumerateFiles(string root, string searchPattern, bool recursive) =>
        _files.Values.Where(f => f.Path.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase)).ToList();

    public CleanupFile? GetFile(string path) => _files.TryGetValue(path, out var f) ? f : null;

    public DeleteOutcome DeleteFile(string path) => _files.Remove(path) ? DeleteOutcome.Deleted : DeleteOutcome.NotFound;

    public void DeleteEmptyDirectories(string root)
    {
    }
}

internal sealed class StaticCatalog(params CleanupCategory[] categories) : ICleanupCatalog
{
    public IReadOnlyList<CleanupCategory> GetCategories() => categories;
}

internal sealed class MemoryHistory : IOperationHistory
{
    public List<OperationRecord> Records { get; } = [];

    public event EventHandler? Changed;

    public Task AddAsync(OperationRecord record, CancellationToken cancellationToken = default)
    {
        Records.Add(record);
        Changed?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<OperationRecord>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<OperationRecord>>(Records.OrderByDescending(r => r.Time).ToList());
}

internal sealed class FakeDialogs(bool answer) : IDialogService
{
    public List<ConfirmationRequest> Requests { get; } = [];

    public bool Confirm(ConfirmationRequest request)
    {
        Requests.Add(request);
        return answer;
    }
}

internal sealed class FakeBin : IRecycleBin
{
    public int EmptyCalls { get; private set; }

    public RecycleBinInfo? Query() => new(2, 2048);

    public bool Empty()
    {
        EmptyCalls++;
        return true;
    }
}

/// <summary>A cleanup setup on an in-memory disk.</summary>
internal sealed class CleanupFixture : IDisposable
{
    public const string Temp = @"C:\Users\teste\AppData\Local\Temp";
    public static readonly DateTimeOffset Old = DateTimeOffset.UtcNow.AddDays(-5);

    public CleanupFixture()
    {
        Files.Add(Temp + @"\a.tmp", Gb.Of(1), Old);
        Files.Add(Temp + @"\b.tmp", Gb.Of(0.5), Old);
        var engine = new CleanupEngine(Files, new PathSafetyValidator([@"C:\Windows"]), Bin, TimeProvider.System);
        Session = new CleanupSession(new StaticCatalog(TempCategory, AdminCategory, BinCategory), engine, History, new OsProfile(WindowsVersion.Windows11, null));
    }

    public static CleanupCategory TempCategory { get; } =
        new("user-temp", "Arquivos temporários do usuário", "d", CleanupGroups.Temp, [new CleanupTarget(Temp)], TimeSpan.FromDays(1), true);

    public static CleanupCategory AdminCategory { get; } =
        new("windows-temp", "Temporários do Windows", "d", CleanupGroups.Temp, [new CleanupTarget(@"C:\Windows\Temp")], TimeSpan.FromDays(1), false, RequiresAdmin: true);

    public static CleanupCategory BinCategory { get; } =
        new("recycle-bin", "Lixeira", "d", CleanupGroups.RecycleBin, [], TimeSpan.Zero, false, Kind: CleanupKind.RecycleBin);

    public MemoryFileSystem Files { get; } = new();

    public FakeBin Bin { get; } = new();

    public MemoryHistory History { get; } = new();

    public CleanupSession Session { get; }

    public void Dispose() => Session.Dispose();
}
