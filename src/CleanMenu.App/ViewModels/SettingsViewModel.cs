using System.IO;
using System.Collections.ObjectModel;
using CleanMenu.App.Navigation;
using CleanMenu.App.Services;
using CleanMenu.Core.Platform;
using CleanMenu.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CleanMenu.App.ViewModels;

/// <summary>A cleanup category checkbox in Settings.</summary>
public sealed partial class DefaultCategoryOption(string id, string title, bool isChecked) : ObservableObject
{
    public string Id { get; } = id;

    public string Title { get; } = title;

    [ObservableProperty]
    private bool _isChecked = isChecked;
}

/// <summary>
/// Configurações. Every change is saved immediately. Theme applies at once; Windows version and
/// refresh rate apply the next time Clean Menu opens (the app says so).
/// </summary>
public sealed partial class SettingsViewModel : PageViewModel
{
    private readonly ISettingsStore _store;
    private readonly IAutoStartService _autoStart;
    private readonly ISystemActions _actions;
    private readonly CleanupSession _cleanup;
    private readonly Action<ThemePreference> _applyTheme;
    private readonly AppSettings _startupSettings;
    private AppSettings _settings;
    private bool _loading = true;

    [ObservableProperty]
    private ThemePreference _theme;

    [ObservableProperty]
    private OsPreference _os;

    [ObservableProperty]
    private int _refreshSeconds;

    [ObservableProperty]
    private bool _analyzeOnStartup;

    [ObservableProperty]
    private bool _confirmBeforeCleaning;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private bool _needsRestart;

    public SettingsViewModel(
        ISettingsStore store,
        IAutoStartService autoStart,
        ISystemActions actions,
        CleanupSession cleanup,
        OsProfile osProfile,
        Action<ThemePreference> applyTheme,
        IUpdateChecker? updates = null)
        : base(PageCatalog.Get(AppPage.Settings))
    {
        _updates = updates;
        ArgumentNullException.ThrowIfNull(osProfile);
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _autoStart = autoStart ?? throw new ArgumentNullException(nameof(autoStart));
        _actions = actions ?? throw new ArgumentNullException(nameof(actions));
        _cleanup = cleanup ?? throw new ArgumentNullException(nameof(cleanup));
        _applyTheme = applyTheme ?? throw new ArgumentNullException(nameof(applyTheme));

        _settings = _startupSettings = store.Load();
        Theme = _settings.Theme;
        Os = _settings.Os;
        RefreshSeconds = _settings.RefreshSeconds;
        AnalyzeOnStartup = _settings.AnalyzeOnStartup;
        ConfirmBeforeCleaning = _settings.ConfirmBeforeCleaning;
        StartWithWindows = SafeAutoStartState();
        DetectedWindows = OsDisplay.Name(osProfile.Detected);

        foreach (var category in cleanup.Categories.Where(c => !c.RequiresAdmin))
        {
            var option = new DefaultCategoryOption(category.Id, category.Title, cleanup.IsSelectedByDefault(category));
            option.PropertyChanged += (_, _) => SaveCategories();
            DefaultCategories.Add(option);
        }

        _loading = false;
    }

    public string DetectedWindows { get; }

    private readonly IUpdateChecker? _updates;

    [ObservableProperty]
    private string? _updateText;

    [ObservableProperty]
    private bool _isCheckingUpdates;

    /// <summary>Version of this build ("0.1.0").</summary>
    public string CurrentVersion { get; } =
        typeof(SettingsViewModel).Assembly.GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "0.0.0";

    [RelayCommand]
    private async Task CheckUpdatesAsync()
    {
        if (_updates is null || IsCheckingUpdates)
        {
            return;
        }

        IsCheckingUpdates = true;
        UpdateText = "Procurando versões novas no GitHub…";
        var result = await _updates.CheckAsync(CurrentVersion).ConfigureAwait(true);
        UpdateText = result.Status switch
        {
            UpdateStatus.NewVersion => $"Existe uma versão nova: {result.LatestVersion}. Clique em \"Abrir página de versões\" para baixar.",
            UpdateStatus.UpToDate => "Você já está usando a versão mais recente.",
            UpdateStatus.NoReleasesYet => "Ainda não há versões publicadas no GitHub.",
            _ => "Não foi possível verificar agora. Confira se a internet está funcionando.",
        };
        IsCheckingUpdates = false;
    }

    [RelayCommand]
    private void OpenReleases()
    {
        if (_updates is not null)
        {
            _actions.OpenTool(_updates.ReleasesPage.AbsoluteUri);
        }
    }

    public string DataFolder => _store.DataFolder;

    public IReadOnlyList<int> RefreshOptions { get; } = AppSettings.AllowedRefreshSeconds;

    public ObservableCollection<DefaultCategoryOption> DefaultCategories { get; } = [];

    partial void OnThemeChanged(ThemePreference value)
    {
        if (_loading)
        {
            return;
        }

        _applyTheme(value);
        Save(_settings with { Theme = value });
    }

    partial void OnOsChanged(OsPreference value) => SaveAndCheckRestart(_settings with { Os = value });

    partial void OnRefreshSecondsChanged(int value) => SaveAndCheckRestart(_settings with { RefreshSeconds = value });

    partial void OnAnalyzeOnStartupChanged(bool value) => Save(_settings with { AnalyzeOnStartup = value });

    partial void OnConfirmBeforeCleaningChanged(bool value)
    {
        if (_loading)
        {
            return;
        }

        _cleanup.ConfirmBeforeCleaning = value;
        Save(_settings with { ConfirmBeforeCleaning = value });
        Message = value ? null : "Atenção: sem confirmação, a limpeza começa assim que você clicar em Limpar. Recomendamos deixar ligado.";
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_loading)
        {
            return;
        }

        try
        {
            _autoStart.SetEnabled(value);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            Message = "Não foi possível alterar a inicialização com o Windows.";
        }
    }

    [RelayCommand]
    private void OpenDataFolder()
    {
        Directory.CreateDirectory(DataFolder);
        _actions.OpenTool(DataFolder);
    }

    private void SaveCategories()
    {
        if (_loading)
        {
            return;
        }

        var ids = DefaultCategories.Where(c => c.IsChecked).Select(c => c.Id).ToList();
        _cleanup.PreferredSelection = ids.ToHashSet(StringComparer.Ordinal);
        Save(_settings with { DefaultCleanupCategories = ids });
    }

    private void SaveAndCheckRestart(AppSettings updated)
    {
        if (_loading)
        {
            return;
        }

        Save(updated);
        NeedsRestart = updated.Os != _startupSettings.Os || updated.RefreshSeconds != _startupSettings.RefreshSeconds;
    }

    private void Save(AppSettings updated)
    {
        if (_loading)
        {
            return;
        }

        _settings = updated;
        try
        {
            _store.Save(updated);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Message = "Não foi possível salvar as configurações.";
        }
    }

    private bool SafeAutoStartState()
    {
        try
        {
            return _autoStart.IsEnabled;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            return false;
        }
    }
}
