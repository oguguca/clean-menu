using System.Windows;
using CleanMenu.App.ViewModels;
using CleanMenu.Core.Platform;
using CleanMenu.Infrastructure.Platform;
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
        // Platform
        services.AddSingleton<IOsVersionProvider, WindowsOsVersionProvider>();
        // The user's own choice (Settings) is wired in Etapa 6; until then the detected version is used.
        services.AddSingleton(sp => OsProfile.Detect(sp.GetRequiredService<IOsVersionProvider>()));

        // Navigation and pages
        services.AddSingleton<IPageFactory, PageFactory>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient(sp => new DashboardViewModel(
            sp.GetRequiredService<OsProfile>(),
            Environment.MachineName,
            sp.GetRequiredService<IOsVersionProvider>().GetOsVersion().Build));

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
