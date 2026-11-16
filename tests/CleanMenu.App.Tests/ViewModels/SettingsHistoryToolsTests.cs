using CleanMenu.App.Services;
using CleanMenu.App.ViewModels;
using CleanMenu.Core.History;
using CleanMenu.Core.Platform;
using CleanMenu.Core.Settings;

namespace CleanMenu.App.Tests.ViewModels;

public sealed class SettingsHistoryToolsTests : IDisposable
{
    private readonly CleanupFixture _cleanup = new();

    public void Dispose() => _cleanup.Dispose();

    private sealed class MemoryStore(AppSettings initial) : ISettingsStore
    {
        public AppSettings Saved { get; private set; } = initial;

        public string DataFolder => @"C:\Dados\CleanMenu";

        public AppSettings Load() => Saved;

        public void Save(AppSettings settings) => Saved = settings;
    }

    private sealed class FakeAutoStart : IAutoStartService
    {
        public bool IsEnabled { get; private set; }

        public void SetEnabled(bool enabled) => IsEnabled = enabled;
    }

    private (SettingsViewModel Vm, MemoryStore Store, List<ThemePreference> Applied, FakeAutoStart AutoStart) Create(AppSettings? initial = null)
    {
        var store = new MemoryStore(initial ?? new AppSettings());
        var applied = new List<ThemePreference>();
        var autoStart = new FakeAutoStart();
        var vm = new SettingsViewModel(store, autoStart, new FakeActions(), _cleanup.Session,
            new OsProfile(WindowsVersion.Windows11, null), applied.Add);
        return (vm, store, applied, autoStart);
    }

    [Fact]
    public void Loading_DoesNotSaveOrApplyAnything()
    {
        var (_, store, applied, _) = Create(new AppSettings { Theme = ThemePreference.Light });

        Assert.Empty(applied);
        Assert.Equal(ThemePreference.Light, store.Saved.Theme);
    }

    [Fact]
    public void Theme_AppliesImmediately_AndIsSaved()
    {
        var (vm, store, applied, _) = Create();

        vm.Theme = ThemePreference.Light;

        Assert.Equal([ThemePreference.Light], applied);
        Assert.Equal(ThemePreference.Light, store.Saved.Theme);
        Assert.False(vm.NeedsRestart);
    }

    [Fact]
    public void WindowsChoice_AndRefresh_AskForRestart()
    {
        var (vm, store, _, _) = Create();

        vm.Os = OsPreference.Windows10;
        Assert.True(vm.NeedsRestart);
        Assert.Equal(WindowsVersion.Windows10, store.Saved.SelectedWindows);

        vm.Os = OsPreference.Automatic;
        Assert.False(vm.NeedsRestart);

        vm.RefreshSeconds = 5;
        Assert.True(vm.NeedsRestart);
        Assert.Equal(5, store.Saved.RefreshSeconds);
    }

    [Fact]
    public void ConfirmationOff_WarnsAndUpdatesSession()
    {
        var (vm, store, _, _) = Create();

        vm.ConfirmBeforeCleaning = false;

        Assert.False(_cleanup.Session.ConfirmBeforeCleaning);
        Assert.False(store.Saved.ConfirmBeforeCleaning);
        Assert.Contains("Recomendamos", vm.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultCategories_ExcludeAdmin_AndUpdateSession()
    {
        var (vm, store, _, _) = Create();

        Assert.DoesNotContain(vm.DefaultCategories, c => c.Id == "windows-temp");
        vm.DefaultCategories.Single(c => c.Id == "recycle-bin").IsChecked = true;

        Assert.Contains("recycle-bin", store.Saved.DefaultCleanupCategories!);
        Assert.True(_cleanup.Session.IsSelectedByDefault(CleanupFixture.BinCategory));
    }

    [Fact]
    public void StartWithWindows_UsesAutoStartService()
    {
        var (vm, _, _, autoStart) = Create();

        vm.StartWithWindows = true;

        Assert.True(autoStart.IsEnabled);
    }

    [Fact]
    public void Sanitize_FixesInvalidValues()
    {
        var fixedSettings = new AppSettings { RefreshSeconds = 37, Theme = (ThemePreference)99 }.Sanitize();

        Assert.Equal(2, fixedSettings.RefreshSeconds);
        Assert.Equal(ThemePreference.Dark, fixedSettings.Theme);
    }

    [Fact]
    public async Task History_ShowsNewestFirst_WithTotals()
    {
        var history = new MemoryHistory();
        await history.AddAsync(new OperationRecord(Guid.NewGuid(), DateTimeOffset.Now.AddDays(-1), "Limpeza", ["Temporários"], 10, Gb.Of(1), 2, [], "Concluída com avisos", false));
        await history.AddAsync(new OperationRecord(Guid.NewGuid(), DateTimeOffset.Now, "Simulação de limpeza", ["Temporários"], 5, Gb.Of(3), 0, [], "Concluída", true));

        var vm = new HistoryViewModel(history, new ImmediateUiDispatcher());
        await vm.LoadAsync();

        Assert.Equal("Simulação de limpeza", vm.Rows[0].Type);
        Assert.Contains("simulação, nada apagado", vm.Rows[0].Details, StringComparison.Ordinal);
        Assert.Contains("2 arquivos mantidos", vm.Rows[1].Result, StringComparison.Ordinal);
        Assert.Contains("Total liberado pelo Clean Menu: 1 GB em 1 limpeza(s)", vm.Summary, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(WindowsVersion.Windows10, "Prompt de Comando", "Terminal")]
    [InlineData(WindowsVersion.Windows11, "Terminal", "Prompt de Comando")]
    public void Tools_OnlyThoseOfChosenWindows(WindowsVersion version, string expected, string absent)
    {
        var vm = new WindowsToolsViewModel(new FakeActions(), new OsProfile(version, null));

        Assert.Contains(vm.Tools, t => t.Title == expected);
        Assert.DoesNotContain(vm.Tools, t => t.Title == absent);
        Assert.Contains(vm.Tools, t => t.Title == "Gerenciador de Tarefas");
        Assert.True(vm.Tools.Count >= 12);
    }

    [Fact]
    public void Tools_OpenReportsFailure()
    {
        var actions = new FakeActions { OpenResult = false };
        var vm = new WindowsToolsViewModel(actions, new OsProfile(WindowsVersion.Windows11, null));

        vm.OpenCommand.Execute(vm.Tools[0]);

        Assert.Equal(["taskmgr.exe"], actions.Opened);
        Assert.NotNull(vm.Message);
    }
}
