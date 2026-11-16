using System.Windows;
using CleanMenu.App.Services;
using CleanMenu.App.ViewModels;
using CleanMenu.Core.Disks;
using CleanMenu.Core.Monitoring;
using CleanMenu.Core.Platform;
using CleanMenu.Core.Processes;
using CleanMenu.Core.SystemInfo;
using CleanMenu.Infrastructure.Disks;
using CleanMenu.Infrastructure.Monitoring;
using CleanMenu.Infrastructure.Platform;
using CleanMenu.Infrastructure.Processes;
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

        // Platform
        services.AddSingleton<IOsVersionProvider, WindowsOsVersionProvider>();
        // The user's own choice (Settings) is wired in Etapa 6; until then the detected version is used.
        services.AddSingleton(sp => OsProfile.Detect(sp.GetRequiredService<IOsVersionProvider>()));

        // Monitoring (read-only)
        services.AddSingleton<IMemoryStatusProvider, WindowsMemoryStatusProvider>();
        services.AddSingleton<ICpuTimesProvider, WindowsCpuTimesProvider>();
        services.AddSingleton<IGpuUsageSampler, WindowsGpuUsageSampler>();
        services.AddSingleton<IProcessSource, WindowsProcessSource>();
        services.AddSingleton<ProcessMonitor>();
        services.AddSingleton<LiveMonitorService>();
        services.AddSingleton<ILiveMonitor>(sp => sp.GetRequiredService<LiveMonitorService>());
        services.AddHostedService(sp => sp.GetRequiredService<LiveMonitorService>());

        // System information and disks (read-only)
        services.AddSingleton<WindowsSystemInfoProvider>();
        services.AddSingleton<ISystemInfoProvider>(sp => new CachedSystemInfoProvider(sp.GetRequiredService<WindowsSystemInfoProvider>()));
        services.AddSingleton<IVolumeProvider, WindowsVolumeProvider>();
        services.AddSingleton<IPhysicalDiskProvider, WindowsPhysicalDiskProvider>();

        // Navigation and pages
        services.AddSingleton<IPageFactory, PageFactory>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient(sp => new DashboardViewModel(
            sp.GetRequiredService<OsProfile>(),
            sp.GetRequiredService<ILiveMonitor>(),
            sp.GetRequiredService<ISystemInfoProvider>(),
            sp.GetRequiredService<IVolumeProvider>(),
            sp.GetRequiredService<IUiDispatcher>(),
            Environment.MachineName,
            sp.GetRequiredService<IOsVersionProvider>().GetOsVersion().Build));
        services.AddTransient<MemoryViewModel>();
        services.AddTransient<ProcessesViewModel>();
        services.AddTransient<SystemInfoViewModel>();
        services.AddTransient<DisksViewModel>();

        // Shell
        services.AddSingleton<MainWindow>();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        await _host.StartAsync().ConfigureAwait(true);

        MainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(true);
        _host.Dispose();
        base.OnExit(e);
    }
}
