using CleanMenu.App.Services;
using CleanMenu.App.ViewModels;

namespace CleanMenu.App.Tests.ViewModels;

public sealed class SmartCleanupViewModelTests : IDisposable
{
    private readonly CleanupFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    private SmartCleanupViewModel Create(bool confirm) =>
        new(_fixture.Session, new FakeDialogs(confirm), new ImmediateUiDispatcher());

    [Fact]
    public void BeforeAnalysis_CleanIsDisabled_AndAdminCategoryCannotBeSelected()
    {
        using var vm = Create(confirm: true);

        Assert.False(vm.CleanCommand.CanExecute(null));
        var admin = vm.Rows.Single(r => r.Category.RequiresAdmin);
        Assert.False(admin.CanSelect);
        Assert.False(admin.IsSelected);
        Assert.Contains("administrador", admin.NoteText, StringComparison.Ordinal);
        Assert.False(vm.Rows.Single(r => r.Title == "Lixeira").IsSelected); // never preselected
    }

    [Fact]
    public async Task Analyze_FillsNumbers_WithoutDeleting()
    {
        using var vm = Create(confirm: true);

        await vm.AnalyzeCommand.ExecuteAsync(null);

        var temp = vm.Rows.Single(r => r.Category.Id == "user-temp");
        Assert.Equal("1,5 GB", temp.RecoverableText);
        Assert.Equal("2 arquivos", temp.CountText);
        Assert.True(temp.HasPreview);
        Assert.Equal(2, _fixture.Files.Files.Count);
        Assert.True(vm.CleanCommand.CanExecute(null));
        Assert.Contains("1,5 GB", vm.SelectionSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Clean_UserCancelsConfirmation_NothingDeleted()
    {
        var dialogs = new FakeDialogs(answer: false);
        using var vm = new SmartCleanupViewModel(_fixture.Session, dialogs, new ImmediateUiDispatcher());
        await vm.AnalyzeCommand.ExecuteAsync(null);

        await vm.CleanCommand.ExecuteAsync(null);

        Assert.Single(dialogs.Requests);
        Assert.Equal(2, _fixture.Files.Files.Count);
        Assert.Empty(_fixture.History.Records);
    }

    [Fact]
    public async Task Clean_Confirmed_DeletesAndRecordsHistory()
    {
        using var vm = Create(confirm: true);
        await vm.AnalyzeCommand.ExecuteAsync(null);

        await vm.CleanCommand.ExecuteAsync(null);

        Assert.Empty(_fixture.Files.Files);
        var record = Assert.Single(_fixture.History.Records);
        Assert.Equal("Limpeza", record.Type);
        Assert.Equal(2, record.FilesRemoved);
        Assert.Contains("1,5 GB liberados", vm.ResultMessage, StringComparison.Ordinal);
        Assert.False(vm.HasAnalysis); // must analyze again before cleaning more
    }

    [Fact]
    public async Task DryRun_SkipsConfirmation_AndDeletesNothing()
    {
        var dialogs = new FakeDialogs(answer: false);
        using var vm = new SmartCleanupViewModel(_fixture.Session, dialogs, new ImmediateUiDispatcher());
        await vm.AnalyzeCommand.ExecuteAsync(null);
        vm.IsDryRun = true;

        await vm.CleanCommand.ExecuteAsync(null);

        Assert.Empty(dialogs.Requests);
        Assert.Equal(2, _fixture.Files.Files.Count);
        Assert.Equal("Simulação de limpeza", Assert.Single(_fixture.History.Records).Type);
        Assert.Contains("nada foi apagado", vm.ResultMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Confirmation_WarnsAboutRecycleBin()
    {
        var dialogs = new FakeDialogs(answer: false);
        using var vm = new SmartCleanupViewModel(_fixture.Session, dialogs, new ImmediateUiDispatcher());
        await vm.AnalyzeCommand.ExecuteAsync(null);
        vm.SelectAllCommand.Execute(null);

        await vm.CleanCommand.ExecuteAsync(null);

        var request = Assert.Single(dialogs.Requests);
        Assert.Contains(request.Details, d => d.Contains("Lixeira", StringComparison.Ordinal) && d.Contains("não poderão ser recuperados", StringComparison.Ordinal));
        Assert.Contains(request.Details, d => d.Contains("senhas", StringComparison.Ordinal));
        Assert.Equal(0, _fixture.Bin.EmptyCalls);
    }

    [Fact]
    public void SelectAll_NeverSelectsAdminCategories()
    {
        using var vm = Create(confirm: true);

        vm.SelectAllCommand.Execute(null);

        Assert.False(vm.Rows.Single(r => r.Category.RequiresAdmin).IsSelected);
        vm.SelectNoneCommand.Execute(null);
        Assert.All(vm.Rows, r => Assert.False(r.IsSelected));
    }
}
