using CleanMenu.App.ViewModels;
using CleanMenu.Core.Platform;
using CleanMenu.UI;

namespace CleanMenu.App.Tests.ViewModels;

public class DashboardViewModelTests
{
    private static DashboardViewModel Create(string machine = "PC-TESTE", int build = 26100) =>
        new(new OsProfile(WindowsVersion.Windows11, null), machine, build);

    [Fact]
    public void AnalyzeCommand_IsDisabled_UntilAnalysisExists()
    {
        var vm = Create();

        Assert.False(vm.IsAnalysisAvailable);
        Assert.False(vm.AnalyzeCommand.CanExecute(null));
    }

    [Fact]
    public void SummaryCards_ShowNoInventedValues()
    {
        var vm = Create();

        // Without a real analysis every card must be empty ("—") or state a fact ("Nunca"), never a number.
        Assert.All(vm.SummaryCards, card =>
        {
            Assert.True(card.Value is "—" or "Nunca", $"Unexpected value '{card.Value}' for '{card.Title}'.");
            Assert.Equal(StatusLevel.Neutral, card.Status);
            Assert.False(string.IsNullOrWhiteSpace(card.HelpText));
        });
    }

    [Fact]
    public void SummaryCards_CoverRequiredTopics()
    {
        var titles = Create().SummaryCards.Select(c => c.Title).ToList();

        Assert.Equal(6, titles.Count);
        Assert.Contains("Saúde geral", titles);
        Assert.Contains("Espaço para liberar", titles);
        Assert.Contains("Memória livre", titles);
        Assert.Contains("Arquivos temporários", titles);
        Assert.Contains("Programas ao ligar", titles);
        Assert.Contains("Última limpeza", titles);
    }

    [Fact]
    public void ComputerFacts_ComeFromInputs()
    {
        var vm = Create("MEU-PC", 22631);

        Assert.Equal("MEU-PC", vm.MachineName);
        Assert.Equal("Windows 11", vm.WindowsName);
        Assert.Equal("Versão 22631", vm.WindowsBuild);
    }

    [Fact]
    public void MissingFacts_ShowDash()
    {
        var vm = Create(" ", 0);

        Assert.Equal("—", vm.MachineName);
        Assert.Equal("—", vm.WindowsBuild);
    }
}
