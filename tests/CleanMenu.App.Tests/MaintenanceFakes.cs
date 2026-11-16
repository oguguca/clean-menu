using CleanMenu.Core.Maintenance;
using CleanMenu.Core.Platform;
using CleanMenu.Core.Processes;
using CleanMenu.Core.Startup;

namespace CleanMenu.App.Tests;

internal sealed class FakeProcessActions : IProcessActions
{
    public List<(int Pid, string Name)> Ended { get; } = [];

    public string? Path { get; set; } = @"C:\Apps\app.exe";

    public string? GetExecutablePath(int pid) => Path;

    public ProcessEndResult EndProcess(int pid, string expectedName)
    {
        if (ProcessClassifier.IsProtected(expectedName, pid))
        {
            return ProcessEndResult.Protected;
        }

        Ended.Add((pid, expectedName));
        return ProcessEndResult.Ended;
    }

    public bool OpenFileLocation(string path) => true;

    public bool ShowFileProperties(string path) => true;
}

internal sealed class FakeRunner(bool result = true) : IMaintenanceRunner
{
    public List<MaintenanceCommand> Started { get; } = [];

    public bool StartVisibleElevated(MaintenanceCommand command)
    {
        Started.Add(command);
        return result;
    }
}

internal sealed class FakeStartupManager(params StartupEntry[] entries) : IStartupManager
{
    public List<(string Name, bool Enabled)> Changes { get; } = [];

    public Task<IReadOnlyList<StartupEntry>> GetEntriesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<StartupEntry>>(entries);

    public void SetEnabled(StartupEntry entry, bool enabled)
    {
        if (!entry.CanToggle)
        {
            throw new InvalidOperationException("admin");
        }

        Changes.Add((entry.Name, enabled));
    }
}

internal sealed class FakePowerPlans : IPowerPlanService
{
    public static readonly Guid Balanced = Guid.Parse("381b4222-f694-41f0-9685-ff5bb260df2e");
    public static readonly Guid High = Guid.Parse("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");

    public Guid Active { get; private set; } = Balanced;

    public Task<IReadOnlyList<PowerPlan>> GetPlansAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PowerPlan>>([new(Balanced, "Equilibrado", Active == Balanced), new(High, "Alto desempenho", Active == High)]);

    public Task<bool> ActivateAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        Active = planId;
        return Task.FromResult(true);
    }
}

internal sealed class FakeSettingsReader : IWindowsSettingsReader
{
    public IReadOnlyList<WindowsSettingInfo> Read(WindowsVersion version) =>
    [
        new("Dados de diagnóstico", "x", "Padrão do Windows", "ms-settings:privacy-feedback", OsScope.All),
        new("Aplicativos em segundo plano (10)", "x", "Ligado", "ms-settings:privacy-backgroundapps", OsScope.Windows10),
        new("Aplicativos em segundo plano (11)", "x", "Por aplicativo", "ms-settings:appsfeatures", OsScope.Windows11),
    ];
}
