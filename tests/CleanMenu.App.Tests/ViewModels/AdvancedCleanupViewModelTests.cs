using System.IO;
using CleanMenu.App.ViewModels;
using CleanMenu.Core.Advanced;

namespace CleanMenu.App.Tests.ViewModels;

public class AdvancedCleanupViewModelTests
{
    private const string Profile = @"C:\Users\teste";

    private sealed class SizeHasher : IFileHasher
    {
        // Files with the same size are treated as identical content in these tests.
        public Task<string?> Sha256Async(string path, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(Path.GetExtension(path));
    }

    private sealed class RecordingRecycler : IRecycler
    {
        public List<string> Recycled { get; } = [];

        public RecycleOutcome SendToRecycleBin(string path)
        {
            Recycled.Add(path);
            return RecycleOutcome.Recycled;
        }
    }

    private sealed class NoLeftovers : ILeftoverSource
    {
        public IReadOnlyList<InstalledProgram> GetInstalledPrograms() => [];

        public IReadOnlyList<LeftoverCandidate> GetAppDataFolders() => [];
    }

    private static (AdvancedCleanupViewModel Vm, RecordingRecycler Recycler, FakeDialogs Dialogs, MemoryHistory History) Create(MemoryFileSystem files, bool confirm = true)
    {
        var recycler = new RecordingRecycler();
        var dialogs = new FakeDialogs(confirm);
        var history = new MemoryHistory();
        var vm = new AdvancedCleanupViewModel(new AdvancedScanner(files, new SizeHasher()), new NoLeftovers(), recycler,
            new CleanMenu.App.Services.FolderPicker(), new FakeProcessActions(), dialogs, history, new FakeActions(),
            [@"C:\Windows", @"C:\Program Files"], Profile, Profile + @"\Downloads");
        return (vm, recycler, dialogs, history);
    }

    [Fact]
    public async Task LargeFiles_ExcludeDownloads_UnlessAllowed()
    {
        var files = new MemoryFileSystem();
        files.Add(Profile + @"\Videos\filme.mkv", Gb.Of(2), DateTimeOffset.UtcNow.AddDays(-200));
        files.Add(Profile + @"\Downloads\backup.zip", Gb.Of(1), DateTimeOffset.UtcNow.AddDays(-200));
        var (vm, _, _, _) = Create(files);

        await vm.ScanCommand.ExecuteAsync(null);
        Assert.Equal(["filme.mkv"], vm.Files.Select(f => f.Name));

        vm.AllowDownloads = true;
        await vm.ScanCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.Files.Count);
    }

    [Fact]
    public async Task ProtectedRoot_IsRefused()
    {
        var (vm, _, _, _) = Create(new MemoryFileSystem());
        vm.ScanRoot = @"C:\Windows\System32";

        await vm.ScanCommand.ExecuteAsync(null);

        Assert.Contains("não pode ser analisada", vm.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Duplicates_MarkCopiesKeepsOldest_AndRecycleNeedsConfirmation()
    {
        var files = new MemoryFileSystem();
        files.Add(Profile + @"\a\foto.jpg", Gb.Of(0.1), DateTimeOffset.UtcNow.AddDays(-400));
        files.Add(Profile + @"\b\foto.jpg", Gb.Of(0.1), DateTimeOffset.UtcNow.AddDays(-200));
        var (vm, recycler, dialogs, history) = Create(files);
        vm.Mode = AdvancedMode.Duplicates;

        await vm.ScanCommand.ExecuteAsync(null);
        var group = Assert.Single(vm.Duplicates);
        Assert.False(vm.RecycleSelectedCommand.CanExecute(null));

        vm.MarkDuplicateCopiesCommand.Execute(null);
        Assert.False(group.Files[0].IsChecked);
        Assert.True(group.Files[1].IsChecked);

        await vm.RecycleSelectedCommand.ExecuteAsync(null);

        Assert.Single(dialogs.Requests);
        Assert.Equal([Profile + @"\b\foto.jpg"], recycler.Recycled);
        Assert.Empty(vm.Duplicates); // group with a single file left is removed
        Assert.Equal("Limpeza avançada (Lixeira)", Assert.Single(history.Records).Type);
    }

    [Fact]
    public async Task Recycle_WarnsWhenAllCopiesOfAGroupAreChecked()
    {
        var files = new MemoryFileSystem();
        files.Add(Profile + @"\a\doc.pdf", Gb.Of(0.1), DateTimeOffset.UtcNow.AddDays(-200));
        files.Add(Profile + @"\b\doc.pdf", Gb.Of(0.1), DateTimeOffset.UtcNow.AddDays(-200));
        var (vm, recycler, dialogs, _) = Create(files, confirm: false);
        vm.Mode = AdvancedMode.Duplicates;
        await vm.ScanCommand.ExecuteAsync(null);

        foreach (var file in vm.Duplicates[0].Files)
        {
            file.IsChecked = true;
        }

        await vm.RecycleSelectedCommand.ExecuteAsync(null);

        Assert.Contains(dialogs.Requests[0].Details, d => d.Contains("TODAS as cópias", StringComparison.Ordinal));
        Assert.Empty(recycler.Recycled);
    }
}
