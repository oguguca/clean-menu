using System.Windows;
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
        services.AddSingleton<IOsVersionProvider, WindowsOsVersionProvider>();
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
