using System.Windows;
using CleanMenu.App.Services;
using CleanMenu.App.ViewModels;
using CleanMenu.Core.Advanced;
using CleanMenu.Core.Cleanup;
using CleanMenu.Core.Disks;
using CleanMenu.Core.History;
using CleanMenu.Core.Maintenance;
using CleanMenu.Core.Startup;
using CleanMenu.Infrastructure.Advanced;
using CleanMenu.Infrastructure.Cleanup;
using CleanMenu.Infrastructure.History;
using CleanMenu.Infrastructure.Maintenance;
using CleanMenu.Infrastructure.Startup;
using CleanMenu.Core.Monitoring;
using CleanMenu.Core.Platform;
using CleanMenu.Core.Processes;
using CleanMenu.Core.Settings;
using CleanMenu.Core.SystemInfo;
using CleanMenu.Infrastructure.Disks;
using CleanMenu.Infrastructure.Monitoring;
using CleanMenu.Infrastructure.Platform;
using CleanMenu.Infrastructure.Processes;
using CleanMenu.Infrastructure.Settings;
using CleanMenu.UI.Theming;
using CleanMenu.Infrastructure.SystemInfo;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CleanMenu.App;

public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(ConfigureServices)
            .Build();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IUiDispatcher, WpfUiDispatcher>();
        services.AddSingleton<ISystemActions, SystemActions>();

        // Settings (read once at startup; pages save changes)
        services.AddSingleton<ISettingsStore, JsonSettingsStore>(_ => new JsonSettingsStore());
        services.AddSingleton(sp => sp.GetRequiredService<ISettingsStore>().Load());
        services.AddSingleton<IAutoStartService>(_ => new RegistryAutoStartService(Environment.ProcessPath ?? "CleanMenu.exe"));

        // Platform: the user's choice in Settings wins over detection.
        services.AddSingleton<IOsVersionProvider, WindowsOsVersionProvider>();
        services.AddSingleton(sp => OsProfile.Detect(
            sp.GetRequiredService<IOsVersionProvider>(),
            sp.GetRequiredService<AppSettings>().SelectedWindows));

        // Monitoring (read-only)
        services.AddSingleton<IMemoryStatusProvider, WindowsMemoryStatusProvider>();
        services.AddSingleton<ICpuTimesProvider, WindowsCpuTimesProvider>();
        services.AddSingleton<IGpuUsageSampler, WindowsGpuUsageSampler>();
        services.AddSingleton<IProcessSource, WindowsProcessSource>();
        services.AddSingleton<ProcessMonitor>();
        services.AddSingleton(sp => new LiveMonitorService(
            sp.GetRequiredService<ICpuTimesProvider>(),
            sp.GetRequiredService<IGpuUsageSampler>(),
            sp.GetRequiredService<IMemoryStatusProvider>(),
            sp.GetRequiredService<ProcessMonitor>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<LiveMonitorService>>())
        {
            Interval = TimeSpan.FromSeconds(sp.GetRequiredService<AppSettings>().RefreshSeconds),
        });
        services.AddSingleton<ILiveMonitor>(sp => sp.GetRequiredService<LiveMonitorService>());
        services.AddHostedService(sp => sp.GetRequiredService<LiveMonitorService>());

        // System information and disks (read-only)
        services.AddSingleton<WindowsSystemInfoProvider>();
        services.AddSingleton<ISystemInfoProvider>(sp => new CachedSystemInfoProvider(sp.GetRequiredService<WindowsSystemInfoProvider>()));
        services.AddSingleton<IVolumeProvider, WindowsVolumeProvider>();
        services.AddSingleton<IPhysicalDiskProvider, WindowsPhysicalDiskProvider>();

        // Cleanup and history
        services.AddSingleton<ICleanupCatalog, WindowsCleanupCatalog>(_ => new WindowsCleanupCatalog());
        services.AddSingleton<ICleanupFileSystem, WindowsCleanupFileSystem>();
        services.AddSingleton<IRecycleBin, WindowsRecycleBin>();
        services.AddSingleton(_ => new PathSafetyValidator(WindowsProtectedFolders.Get()));
        services.AddSingleton<CleanupEngine>();
        services.AddSingleton<IOperationHistory, JsonOperationHistory>(_ => new JsonOperationHistory());
        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<AppSettings>();
            return new CleanupSession(
                sp.GetRequiredService<ICleanupCatalog>(),
                sp.GetRequiredService<CleanupEngine>(),
                sp.GetRequiredService<IOperationHistory>(),
                sp.GetRequiredService<OsProfile>())
            {
                ConfirmBeforeCleaning = settings.ConfirmBeforeCleaning,
                PreferredSelection = settings.DefaultCleanupCategories?.ToHashSet(StringComparer.Ordinal),
            };
        });
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<INavigationService, NavigationService>();

        // Startup, processes and maintenance
        services.AddSingleton<IStartupManager, WindowsStartupManager>();
        services.AddSingleton<IProcessActions, WindowsProcessActions>();
        services.AddSingleton<IPowerPlanService, WindowsPowerPlanService>();
        services.AddSingleton<IWindowsSettingsReader, WindowsSettingsReader>();
        services.AddSingleton<IMaintenanceRunner, VisibleElevatedRunner>();

        // Navigation and pages
        services.AddSingleton<IPageFactory, PageFactory>();
        services.AddSingleton(sp => new MainViewModel(
            sp.GetRequiredService<IPageFactory>(),
            sp.GetRequiredService<OsProfile>(),
            sp.GetRequiredService<INavigationService>()));
        services.AddTransient(sp => new DashboardViewModel(
            sp.GetRequiredService<OsProfile>(),
            sp.GetRequiredService<ILiveMonitor>(),
            sp.GetRequiredService<ISystemInfoProvider>(),
            sp.GetRequiredService<IVolumeProvider>(),
            sp.GetRequiredService<IUiDispatcher>(),
            new DashboardAnalysisServices(
                sp.GetRequiredService<CleanupSession>(),
                sp.GetRequiredService<INavigationService>(),
                sp.GetRequiredService<IOperationHistory>(),
                sp.GetRequiredService<IStartupManager>()),
            Environment.MachineName,
            sp.GetRequiredService<IOsVersionProvider>().GetOsVersion().Build));
        services.AddTransient<SmartCleanupViewModel>();
        services.AddTransient<StartupViewModel>();
        services.AddTransient<OptimizationViewModel>();
        services.AddTransient<MemoryViewModel>();
        services.AddTransient<ProcessesViewModel>();
        services.AddTransient<SystemInfoViewModel>();
        services.AddTransient<DisksViewModel>();
        services.AddTransient<HistoryViewModel>();

        // Advanced cleanup (files go to the Recycle Bin, never deleted for good)
        services.AddSingleton<IFileHasher, Sha256FileHasher>();
        services.AddSingleton<AdvancedScanner>();
        services.AddSingleton<ILeftoverSource, WindowsLeftoverSource>();
        services.AddSingleton<IRecycler, ShellRecycler>();
        services.AddSingleton<IFolderPicker, FolderPicker>();
        services.AddTransient(sp => new AdvancedCleanupViewModel(
            sp.GetRequiredService<AdvancedScanner>(),
            sp.GetRequiredService<ILeftoverSource>(),
            sp.GetRequiredService<IRecycler>(),
            sp.GetRequiredService<IFolderPicker>(),
            sp.GetRequiredService<IProcessActions>(),
            sp.GetRequiredService<IDialogService>(),
            sp.GetRequiredService<IOperationHistory>(),
            sp.GetRequiredService<ISystemActions>(),
            AdvancedProtectedFolders.Get(),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));
        services.AddTransient<WindowsToolsViewModel>();
        services.AddTransient(sp => new SettingsViewModel(
            sp.GetRequiredService<ISettingsStore>(),
            sp.GetRequiredService<IAutoStartService>(),
            sp.GetRequiredService<ISystemActions>(),
            sp.GetRequiredService<CleanupSession>(),
            sp.GetRequiredService<OsProfile>(),
            theme => ThemeManager.Apply(Current, theme == ThemePreference.Light ? AppTheme.Light : AppTheme.Dark)));

        // Shell
        services.AddSingleton<MainWindow>();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settings = _host.Services.GetRequiredService<AppSettings>();
        if (settings.Theme == ThemePreference.Light)
        {
            ThemeManager.Apply(this, AppTheme.Light);
        }

        await _host.StartAsync().ConfigureAwait(true);

        MainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow.Show();

        if (settings.AnalyzeOnStartup && _host.Services.GetRequiredService<MainViewModel>().CurrentPage is DashboardViewModel dashboard)
        {
            dashboard.AnalyzeCommand.Execute(null);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
        _host.Dispose();
        base.OnExit(e);
    }
}
