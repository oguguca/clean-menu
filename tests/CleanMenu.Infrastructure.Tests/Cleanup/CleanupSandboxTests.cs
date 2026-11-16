using System.Diagnostics;
using CleanMenu.Core.Cleanup;
using CleanMenu.Core.History;
using CleanMenu.Infrastructure.Cleanup;
using CleanMenu.Infrastructure.History;

namespace CleanMenu.Infrastructure.Tests.Cleanup;

/// <summary>
/// Integration tests on the real file system, confined to a sandbox folder that each test creates
/// under %TEMP% and removes afterwards. No file outside the sandbox is ever deleted.
/// </summary>
public sealed class CleanupSandboxTests : IDisposable
{
    private readonly string _sandbox = Path.Combine(Path.GetTempPath(), "CleanMenuTests", Guid.NewGuid().ToString("N"));
    private readonly string _root;
    private readonly string _outside;

    public CleanupSandboxTests()
    {
        _root = Path.Combine(_sandbox, "limpar");
        _outside = Path.Combine(_sandbox, "fora");
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(_outside);
    }

    public void Dispose()
    {
        // Remove junctions first (without following them), then the sandbox.
        foreach (var dir in Directory.EnumerateDirectories(_sandbox, "*", SearchOption.AllDirectories)
                     .Where(d => new DirectoryInfo(d).Attributes.HasFlag(FileAttributes.ReparsePoint)).ToList())
        {
            Directory.Delete(dir);
        }

        Directory.Delete(_sandbox, recursive: true);
    }

    private static string CreateFile(string folder, string name, int sizeBytes, TimeSpan age)
    {
        var path = Path.Combine(folder, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[sizeBytes]);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow - age);
        return path;
    }

    private static bool CreateJunction(string link, string target)
    {
        using var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;
        process.WaitForExit();
        return process.ExitCode == 0 && Directory.Exists(link);
    }

    private CleanupEngine Engine() =>
        new(new WindowsCleanupFileSystem(), new PathSafetyValidator([_outside]), new NeverEmptyRecycleBin(), TimeProvider.System);

    private CleanupCategory Category() =>
        new("sandbox", "Sandbox", "d", CleanupGroups.Temp, [new CleanupTarget(_root)], TimeSpan.FromHours(1), true);

    [Fact]
    public async Task Clean_RemovesOldFiles_KeepsRecentOnes_AndEmptyFolders()
    {
        var old = CreateFile(_root, @"sub\velho.tmp", 1000, TimeSpan.FromDays(3));
        var recent = CreateFile(_root, "novo.tmp", 500, TimeSpan.Zero);
        var outsideFile = CreateFile(_outside, "pessoal.docx", 100, TimeSpan.FromDays(30));
        var engine = Engine();

        var analysis = engine.AnalyzeCategory(Category());
        var report = await engine.CleanAsync([analysis], dryRun: false);

        Assert.Equal(1, report.DeletedCount);
        Assert.False(File.Exists(old));
        Assert.False(Directory.Exists(Path.Combine(_root, "sub")));
        Assert.True(File.Exists(recent));
        Assert.True(File.Exists(outsideFile));
        Assert.True(Directory.Exists(_root));
    }

    [Fact]
    public async Task Junctions_AreNeverFollowed()
    {
        var target = CreateFile(_outside, "documento.docx", 100, TimeSpan.FromDays(30));
        if (!CreateJunction(Path.Combine(_root, "atalho"), _outside))
        {
            return; // Junctions unavailable on this file system; nothing to verify.
        }

        var engine = Engine();
        var analysis = engine.AnalyzeCategory(Category());
        await engine.CleanAsync([analysis], dryRun: false);

        Assert.DoesNotContain(analysis.Files, f => f.Path.Contains("atalho", StringComparison.OrdinalIgnoreCase));
        Assert.True(File.Exists(target));
        Assert.True(Directory.Exists(Path.Combine(_root, "atalho")));
    }

    [Fact]
    public async Task LockedFile_IsSkippedAsInUse()
    {
        var locked = CreateFile(_root, "aberto.tmp", 100, TimeSpan.FromDays(2));
        var engine = Engine();
        var analysis = engine.AnalyzeCategory(Category());

        CleanupReport report;
        using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            report = await engine.CleanAsync([analysis], dryRun: false);
        }

        Assert.Equal(0, report.DeletedCount);
        Assert.Contains(report.Categories[0].Skipped, s => s.Reason.Contains("uso", StringComparison.Ordinal));
        Assert.True(File.Exists(locked));
    }

    [Fact]
    public async Task ReadOnlyTempFile_IsRemoved()
    {
        var file = CreateFile(_root, "somente-leitura.tmp", 100, TimeSpan.FromDays(2));
        File.SetAttributes(file, FileAttributes.ReadOnly);
        var engine = Engine();

        await engine.CleanAsync([engine.AnalyzeCategory(Category())], dryRun: false);

        Assert.False(File.Exists(file));
    }

    [Fact]
    public async Task DryRun_LeavesEverything()
    {
        var file = CreateFile(_root, "velho.tmp", 100, TimeSpan.FromDays(2));
        var engine = Engine();

        var report = await engine.CleanAsync([engine.AnalyzeCategory(Category())], dryRun: true);

        Assert.Equal(1, report.DeletedCount);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void Catalog_EveryUsableRoot_PassesSafetyRules_OnThisMachine()
    {
        var validator = new PathSafetyValidator(WindowsProtectedFolders.Get());
        var categories = new WindowsCleanupCatalog().GetCategories();

        Assert.Contains(categories, c => c.Id == "user-temp");
        Assert.Contains(categories, c => c.Kind == CleanupKind.RecycleBin);
        foreach (var category in categories.Where(c => !c.RequiresAdmin && c.Kind == CleanupKind.Files))
        {
            Assert.All(category.Targets, t => Assert.Equal(PathRejection.None, validator.ValidateRoot(t.Root)));
        }
    }

    [Fact]
    public void Catalog_BrowserTargets_AreOnlyCacheFolders()
    {
        var local = Path.Combine(_sandbox, "Local");
        var chromeDefault = Path.Combine(local, "Google", "Chrome", "User Data", "Default");
        Directory.CreateDirectory(chromeDefault);
        Directory.CreateDirectory(Path.Combine(local, "Google", "Chrome", "User Data", "Profile 2"));
        Directory.CreateDirectory(Path.Combine(local, "Mozilla", "Firefox", "Profiles", "abc.default"));

        var catalog = new WindowsCleanupCatalog(local, Path.Combine(_sandbox, "Temp"), @"C:\Windows");
        var browserTargets = catalog.GetCategories()
            .Where(c => c.Group == CleanupGroups.Browsers)
            .SelectMany(c => c.Targets)
            .Select(t => Path.GetFileName(t.Root))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Subset(new HashSet<string>(["Cache", "Code Cache", "GPUCache", "ShaderCache", "GrShaderCache", "cache2", "startupCache"], StringComparer.OrdinalIgnoreCase), browserTargets);
        Assert.Contains("Cache", browserTargets);
        Assert.Contains("cache2", browserTargets);
    }

    [Fact]
    public async Task History_AppendsAndReads_SkippingDamagedLines()
    {
        var file = Path.Combine(_sandbox, "history.jsonl");
        using var history = new JsonOperationHistory(file);
        var record = new OperationRecord(Guid.NewGuid(), DateTimeOffset.Now, "Limpeza", ["Temporários"], 3, 300, 1, [], "Concluída", false);

        await history.AddAsync(record);
        await File.AppendAllTextAsync(file, "{isto não é json\n");
        await history.AddAsync(record with { Id = Guid.NewGuid(), Time = record.Time.AddMinutes(1) });

        var all = await history.GetAllAsync();
        Assert.Equal(2, all.Count);
        Assert.True(all[0].Time > all[1].Time);
        Assert.Equal(["Temporários"], all[1].Categories);
    }

    [Fact]
    public void RecycleBin_QueryIsReadOnly()
    {
        // Only reads. Empty() is never called by tests.
        var info = new WindowsRecycleBin().Query();

        Assert.True(info is null || (info.ItemCount >= 0 && info.SizeBytes >= 0));
    }

    private sealed class NeverEmptyRecycleBin : IRecycleBin
    {
        public RecycleBinInfo? Query() => new(0, 0);

        public bool Empty() => throw new InvalidOperationException("Tests must never empty the real Recycle Bin.");
    }
}
