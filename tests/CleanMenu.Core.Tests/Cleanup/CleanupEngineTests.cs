using CleanMenu.Core.Cleanup;
using CleanMenu.Core.History;

namespace CleanMenu.Core.Tests.Cleanup;

public class CleanupEngineTests
{
    private const string Temp = @"C:\Users\ana\AppData\Local\Temp";
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Old = Now.AddDays(-10);
    private static readonly DateTimeOffset Recent = Now.AddMinutes(-5);

    private readonly FakeCleanupFileSystem _fs = new();
    private readonly FakeRecycleBin _bin = new(new RecycleBinInfo(3, 3000));
    private readonly ManualClock _clock = new(Now);
    private readonly CleanupEngine _engine;

    private static readonly CleanupCategory TempCategory = new(
        "user-temp", "Temporários", "desc", CleanupGroups.Temp, [new CleanupTarget(Temp)], TimeSpan.FromDays(1), true);

    public CleanupEngineTests()
    {
        var validator = new PathSafetyValidator([@"C:\Windows", @"C:\Users\ana\Documents"]);
        _engine = new CleanupEngine(_fs, validator, _bin, _clock);
    }

    [Fact]
    public void Analyze_KeepsRecentSystemAndLinkedFiles()
    {
        _fs.Add(Temp + @"\velho.tmp", 100, Old);
        _fs.Add(Temp + @"\novo.tmp", 200, Recent);
        _fs.Add(Temp + @"\sistema.tmp", 300, Old, FileAttributes.System);
        _fs.Add(Temp + @"\link.tmp", 400, Old, FileAttributes.ReparsePoint);

        var result = _engine.AnalyzeCategory(TempCategory);

        Assert.Equal(4, result.TotalFileCount);
        Assert.Equal(1000, result.TotalBytes);
        var file = Assert.Single(result.Files);
        Assert.Equal(Temp + @"\velho.tmp", file.Path);
        Assert.Equal(100, result.RecoverableBytes);
    }

    [Fact]
    public void Analyze_KeepsOldTempFilesThatAProgramOpenedRecently()
    {
        _fs.Put(new CleanupFile(Temp + @"\em-uso.tmp", 100, Old, FileAttributes.Normal, LastAccessUtc: Recent));
        _fs.Add(Temp + @"\esquecido.tmp", 100, Old);

        var result = _engine.AnalyzeCategory(TempCategory);

        Assert.Equal([Temp + @"\esquecido.tmp"], result.Files.Select(f => f.Path));
    }

    [Fact]
    public void Analyze_NeverIncludesCredentialFiles()
    {
        _fs.Add(Temp + @"\Network\Cookies", 100, Old);
        _fs.Add(Temp + @"\Login Data", 100, Old);
        _fs.Add(Temp + @"\ok.tmp", 100, Old);

        var result = _engine.AnalyzeCategory(TempCategory);

        Assert.Equal([Temp + @"\ok.tmp"], result.Files.Select(f => f.Path));
    }

    [Fact]
    public void Analyze_RootContainingProtectedFolder_IsSkippedEntirely()
    {
        var engine = new CleanupEngine(_fs, new PathSafetyValidator([Temp + @"\guardado"]), _bin, _clock);
        _fs.Add(Temp + @"\guardado\importante.tmp", 100, Old);
        _fs.Add(Temp + @"\ok.tmp", 100, Old);

        var result = engine.AnalyzeCategory(TempCategory);

        Assert.Empty(result.Files);
    }

    [Fact]
    public void Analyze_SkipsUnsafeRoots()
    {
        _fs.Add(@"C:\Users\ana\Documents\contrato.docx", 100, Old);
        var badCategory = TempCategory with { Targets = [new CleanupTarget(@"C:\Users\ana\Documents")] };

        var result = _engine.AnalyzeCategory(badCategory);

        Assert.Empty(result.Files);
        Assert.Equal(0, result.TotalFileCount);
    }

    [Fact]
    public void Analyze_AdminCategory_IsNotScanned()
    {
        _fs.Add(@"C:\Windows\Temp\a.tmp", 100, Old);
        var admin = TempCategory with { Targets = [new CleanupTarget(@"C:\Windows\Temp")], RequiresAdmin = true };

        var result = _engine.AnalyzeCategory(admin);

        Assert.Empty(result.Files);
        Assert.NotNull(result.Note);
    }

    [Fact]
    public void Analyze_RecycleBin_UsesWindowsNumbers()
    {
        var bin = new CleanupCategory("recycle-bin", "Lixeira", "d", CleanupGroups.RecycleBin, [], TimeSpan.Zero, false, Kind: CleanupKind.RecycleBin);

        var result = _engine.AnalyzeCategory(bin);

        Assert.Equal(3, result.EffectiveRecoverableCount);
        Assert.Equal(3000, result.EffectiveRecoverableBytes);
    }

    [Fact]
    public async Task Clean_DeletesOnlyAnalyzedEligibleFiles_AndReportsSkips()
    {
        _fs.Add(Temp + @"\a.tmp", 100, Old);
        _fs.Add(Temp + @"\b.tmp", 200, Old);
        _fs.Add(Temp + @"\preso.tmp", 300, Old);
        _fs.Add(Temp + @"\negado.tmp", 400, Old);
        _fs.Locked.Add(Temp + @"\preso.tmp");
        _fs.Denied.Add(Temp + @"\negado.tmp");
        var analysis = _engine.AnalyzeCategory(TempCategory);

        _fs.Add(Temp + @"\criado-depois.tmp", 999, Old); // appeared after analysis: not deleted

        var report = await _engine.CleanAsync([analysis], dryRun: false);

        Assert.Equal(2, report.DeletedCount);
        Assert.Equal(300, report.DeletedBytes);
        Assert.Equal(2, report.SkippedCount);
        Assert.Contains(Temp + @"\criado-depois.tmp", _fs.Files);
        Assert.Contains(Temp + @"\preso.tmp", _fs.Files);
        Assert.Contains(report.Categories[0].Skipped, s => s.Reason.Contains("uso", StringComparison.Ordinal));
        Assert.Equal([Temp], _fs.EmptyDirectoryCleanups);
    }

    [Fact]
    public async Task Clean_FileModifiedAfterAnalysis_IsKept()
    {
        _fs.Add(Temp + @"\a.tmp", 100, Old);
        var analysis = _engine.AnalyzeCategory(TempCategory);
        _fs.Touch(Temp + @"\a.tmp", Now); // a program started using it again

        var report = await _engine.CleanAsync([analysis], dryRun: false);

        Assert.Equal(0, report.DeletedCount);
        Assert.Contains(Temp + @"\a.tmp", _fs.Files);
    }

    [Fact]
    public async Task Clean_TamperedAnalysisPath_IsRefused()
    {
        _fs.Add(@"C:\Users\ana\Documents\contrato.docx", 100, Old);
        var tampered = new CategoryAnalysis(TempCategory,
            [new CleanupFile(@"C:\Users\ana\Documents\contrato.docx", 100, Old, FileAttributes.Normal)], 1, 100);

        var report = await _engine.CleanAsync([tampered], dryRun: false);

        Assert.Equal(0, report.DeletedCount);
        Assert.Empty(_fs.DeleteCalls);
        Assert.Contains(@"C:\Users\ana\Documents\contrato.docx", _fs.Files);
    }

    [Fact]
    public async Task DryRun_DeletesNothing_ButReportsWhatWouldBeRemoved()
    {
        _fs.Add(Temp + @"\a.tmp", 100, Old);
        _fs.Add(Temp + @"\b.tmp", 200, Old);
        var bin = new CleanupCategory("recycle-bin", "Lixeira", "d", CleanupGroups.RecycleBin, [], TimeSpan.Zero, false, Kind: CleanupKind.RecycleBin);
        var analyses = new[] { _engine.AnalyzeCategory(TempCategory), _engine.AnalyzeCategory(bin) };

        var report = await _engine.CleanAsync(analyses, dryRun: true);

        Assert.True(report.DryRun);
        Assert.Equal(5, report.DeletedCount);   // 2 files + 3 recycle bin items
        Assert.Empty(_fs.DeleteCalls);
        Assert.Empty(_fs.EmptyDirectoryCleanups);
        Assert.Equal(0, _bin.EmptyCalls);
        Assert.Equal(2, _fs.Files.Count);
    }

    [Fact]
    public async Task Clean_RecycleBin_CallsWindowsApi()
    {
        var bin = new CleanupCategory("recycle-bin", "Lixeira", "d", CleanupGroups.RecycleBin, [], TimeSpan.Zero, false, Kind: CleanupKind.RecycleBin);

        var report = await _engine.CleanAsync([_engine.AnalyzeCategory(bin)], dryRun: false);

        Assert.Equal(1, _bin.EmptyCalls);
        Assert.Equal(3000, report.DeletedBytes);
    }

    [Fact]
    public async Task Clean_Cancelled_StopsAndSaysSo()
    {
        for (var i = 0; i < 10; i++)
        {
            _fs.Add($@"{Temp}\f{i}.tmp", 10, Old);
        }

        var analysis = _engine.AnalyzeCategory(TempCategory);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var report = await _engine.CleanAsync([analysis], dryRun: false, cancellationToken: cts.Token);

        Assert.True(report.Cancelled);
        Assert.Equal(10, _fs.Files.Count);
    }

    [Fact]
    public async Task Analyze_ReportsProgress_AndCanBeCancelled()
    {
        var steps = new List<OperationProgress>();
        var progress = new SynchronousProgress(steps);

        await _engine.AnalyzeAsync([TempCategory], progress);
        Assert.Contains(steps, s => s.Step.Contains("Temporários", StringComparison.Ordinal));

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _engine.AnalyzeAsync([TempCategory], null, cts.Token));
    }

    [Fact]
    public async Task Report_BecomesHistoryRecord()
    {
        _fs.Add(Temp + @"\a.tmp", 100, Old);
        var report = await _engine.CleanAsync([_engine.AnalyzeCategory(TempCategory)], dryRun: false);

        var record = OperationRecords.FromCleanup(report);

        Assert.Equal("Limpeza", record.Type);
        Assert.Equal(1, record.FilesRemoved);
        Assert.Equal(100, record.BytesRecovered);
        Assert.Equal("Concluída", record.Result);
        Assert.Equal(["Temporários"], record.Categories);
    }

    private sealed class SynchronousProgress(List<OperationProgress> steps) : IProgress<OperationProgress>
    {
        public void Report(OperationProgress value) => steps.Add(value);
    }
}
