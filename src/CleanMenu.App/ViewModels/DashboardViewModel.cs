using CleanMenu.App.Navigation;
using CleanMenu.Core.Platform;
using CleanMenu.UI;
using CommunityToolkit.Mvvm.Input;

namespace CleanMenu.App.ViewModels;

/// <summary>
/// Home page. Only shows information that is really known. Every metric that depends on the
/// computer analysis stays empty ("—") until that analysis exists and runs.
/// </summary>
public sealed partial class DashboardViewModel : PageViewModel
{
    private const string Pending = "—";
    private const string AfterAnalysis = "Aparece depois da análise";

    public DashboardViewModel(OsProfile osProfile, string machineName, int osBuild)
        : base(PageCatalog.Get(AppPage.Dashboard))
    {
        ArgumentNullException.ThrowIfNull(osProfile);

        MachineName = string.IsNullOrWhiteSpace(machineName) ? Pending : machineName;
        WindowsName = OsDisplay.Name(osProfile.Effective);
        WindowsBuild = osBuild > 0 ? $"Versão {osBuild}" : Pending;

        SummaryCards =
        [
            new("Saúde geral", Glyphs.CheckMark, Pending, AfterAnalysis,
                "Um resumo de como está o computador, calculado a partir do que a análise encontrar: espaço livre, memória e programas que abrem sozinhos."),
            new("Espaço para liberar", Glyphs.Delete, Pending, AfterAnalysis,
                "Quanto espaço os arquivos inúteis (temporários e cache) estão ocupando no seu disco e podem ser apagados com segurança."),
            new("Memória livre", Glyphs.Memory, Pending, "Aparece em breve",
                "Memória RAM que ainda está livre para os programas. Quando ela acaba, o computador fica lento."),
            new("Arquivos temporários", Glyphs.Page, Pending, AfterAnalysis,
                "Arquivos que os programas criam para uso rápido e depois esquecem de apagar."),
            new("Programas ao ligar", Glyphs.Power, Pending, AfterAnalysis,
                "Programas que abrem sozinhos quando o computador liga. Muitos deles deixam o início lento."),
            new("Última limpeza", Glyphs.History, "Nunca", "Nenhuma limpeza feita pelo Clean Menu",
                "Quando o Clean Menu limpou o seu computador pela última vez."),
        ];
    }

    public string MachineName { get; }

    public string WindowsName { get; }

    public string WindowsBuild { get; }

    public IReadOnlyList<SummaryCardModel> SummaryCards { get; }

    /// <summary>False until the analysis engine exists. The button stays visible but disabled.</summary>
    public bool IsAnalysisAvailable { get; }

    public string AnalysisHeadline { get; } = "Seu computador ainda não foi analisado";

    public string AnalysisHint { get; } = "A análise só olha o computador. Nada é apagado ou alterado sem você confirmar.";

    public string AnalysisUnavailableNote { get; } = "A análise fica disponível em uma próxima versão.";

    // Placeholder until the analysis engine exists (Etapa 4). CanExecute is false, so it never runs.
    [RelayCommand(CanExecute = nameof(IsAnalysisAvailable))]
    private Task AnalyzeAsync() => Task.CompletedTask;
}
