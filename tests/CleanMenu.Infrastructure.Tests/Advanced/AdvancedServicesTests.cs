using CleanMenu.Core.Advanced;
using CleanMenu.Infrastructure.Advanced;

namespace CleanMenu.Infrastructure.Tests.Advanced;

/// <summary>Sandbox only. Recycling is tested only for refusals, so the real Recycle Bin is untouched.</summary>
public sealed class AdvancedServicesTests : IDisposable
{
    private readonly string _sandbox = Path.Combine(Path.GetTempPath(), "CleanMenuTests", Guid.NewGuid().ToString("N"));

    public AdvancedServicesTests() => Directory.CreateDirectory(_sandbox);

    public void Dispose() => Directory.Delete(_sandbox, recursive: true);

    [Fact]
    public async Task Hasher_SameContent_SameHash()
    {
        var a = Path.Combine(_sandbox, "a.bin");
        var b = Path.Combine(_sandbox, "b.bin");
        var c = Path.Combine(_sandbox, "c.bin");
        await File.WriteAllBytesAsync(a, [1, 2, 3, 4]);
        await File.WriteAllBytesAsync(b, [1, 2, 3, 4]);
        await File.WriteAllBytesAsync(c, [1, 2, 3, 5]);
        var hasher = new Sha256FileHasher();

        var ha = await hasher.Sha256Async(a, CancellationToken.None);
        Assert.Equal(64, ha!.Length);
        Assert.Equal(ha, await hasher.Sha256Async(b, CancellationToken.None));
        Assert.NotEqual(ha, await hasher.Sha256Async(c, CancellationToken.None));
        Assert.Null(await hasher.Sha256Async(Path.Combine(_sandbox, "nao-existe.bin"), CancellationToken.None));
    }

    [Fact]
    public void Recycler_RefusesFoldersSystemPathsAndNetwork()
    {
        var recycler = new ShellRecycler();

        Assert.Equal(RecycleOutcome.Refused, recycler.SendToRecycleBin(_sandbox));
        Assert.Equal(RecycleOutcome.Refused, recycler.SendToRecycleBin(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe")));
        Assert.Equal(RecycleOutcome.Refused, recycler.SendToRecycleBin(@"\\servidor\share\x.txt"));
        Assert.Equal(RecycleOutcome.Refused, recycler.SendToRecycleBin("relativo.txt"));
        Assert.Equal(RecycleOutcome.NotFound, recycler.SendToRecycleBin(Path.Combine(_sandbox, "nao-existe.txt")));
        Assert.True(File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe")));
    }

    [Fact]
    public void LeftoverSource_ReadsInstalledProgramsAndFolders()
    {
        var source = new WindowsLeftoverSource();

        Assert.NotEmpty(source.GetInstalledPrograms());
        Assert.All(source.GetAppDataFolders(), f => Assert.True(Directory.Exists(f.Path)));
    }
}
