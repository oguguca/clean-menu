using CleanMenu.Core.Advanced;
using CleanMenu.Core.Tests.Cleanup;

namespace CleanMenu.Core.Tests.Advanced;

public class AdvancedScannerTests
{
    private const string Root = @"C:\Users\ana";
    private const long Mb = 1024L * 1024;
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private sealed class ContentHasher(Dictionary<string, string?> contents) : IFileHasher
    {
        public int Calls { get; private set; }

        public Task<string?> Sha256Async(string path, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(contents.GetValueOrDefault(path));
        }
    }

    [Fact]
    public void LargeFiles_BiggestFirst_AboveThreshold_SkippingExcluded()
    {
        var fs = new FakeCleanupFileSystem();
        fs.Add(Root + @"\Videos\filme.mkv", 4000 * Mb, T0);
        fs.Add(Root + @"\Documents\iso.iso", 700 * Mb, T0);
        fs.Add(Root + @"\Documents\pequeno.txt", 1 * Mb, T0);
        fs.Add(Root + @"\Downloads\setup.exe", 900 * Mb, T0);
        fs.Add(Root + @"\link.bin", 5000 * Mb, T0, FileAttributes.ReparsePoint);
        var scanner = new AdvancedScanner(fs, new ContentHasher([]));

        var result = scanner.FindLargeFiles(Root, [Root + @"\Downloads"]);

        Assert.Equal([Root + @"\Videos\filme.mkv", Root + @"\Documents\iso.iso"], result.Select(f => f.Path));
    }

    [Fact]
    public async Task Duplicates_RequireSameSizeAndSameHash()
    {
        var fs = new FakeCleanupFileSystem();
        fs.Add(Root + @"\a\foto.jpg", 5 * Mb, T0.AddDays(2));
        fs.Add(Root + @"\b\foto (1).jpg", 5 * Mb, T0);            // same content, older
        fs.Add(Root + @"\c\outra.jpg", 5 * Mb, T0);                 // same size, different content
        fs.Add(Root + @"\d\bloqueado.jpg", 5 * Mb, T0);             // unreadable
        fs.Add(Root + @"\e\unico.mp4", 9 * Mb, T0);                 // unique size: never hashed
        var hasher = new ContentHasher(new()
        {
            [Root + @"\a\foto.jpg"] = "AAA",
            [Root + @"\b\foto (1).jpg"] = "AAA",
            [Root + @"\c\outra.jpg"] = "BBB",
            [Root + @"\d\bloqueado.jpg"] = null,
        });
        var scanner = new AdvancedScanner(fs, hasher);

        var groups = await scanner.FindDuplicatesAsync(Root, []);

        var group = Assert.Single(groups);
        Assert.Equal([Root + @"\b\foto (1).jpg", Root + @"\a\foto.jpg"], group.Files.Select(f => f.Path)); // oldest first
        Assert.Equal(5 * Mb, group.WastedBytes);
        Assert.Equal(4, hasher.Calls);
    }

    [Fact]
    public async Task Duplicates_IgnoreTinyFiles()
    {
        var fs = new FakeCleanupFileSystem();
        fs.Add(Root + @"\x.txt", 10, T0);
        fs.Add(Root + @"\y.txt", 10, T0);
        var hasher = new ContentHasher([]);

        var groups = await new AdvancedScanner(fs, hasher).FindDuplicatesAsync(Root, []);

        Assert.Empty(groups);
        Assert.Equal(0, hasher.Calls);
    }

    [Fact]
    public void Folders_AggregateDirectChildren()
    {
        var fs = new FakeCleanupFileSystem();
        fs.Add(Root + @"\Videos\a.mkv", 300 * Mb, T0);
        fs.Add(Root + @"\Videos\sub\b.mkv", 200 * Mb, T0);
        fs.Add(Root + @"\Music\c.mp3", 50 * Mb, T0);
        fs.Add(Root + @"\solto.zip", 10 * Mb, T0);

        var folders = new AdvancedScanner(fs, new ContentHasher([])).FindLargestFolders(Root, []);

        Assert.Equal(Root + @"\Videos", folders[0].Path);
        Assert.Equal(500 * Mb, folders[0].SizeBytes);
        Assert.Equal(2, folders[0].FileCount);
        Assert.Contains(folders, f => f.Path == Root && f.SizeBytes == 10 * Mb);
    }

    [Fact]
    public void Leftovers_OnlyOldFolders_NotMatchingInstalledOrSystem()
    {
        var now = new DateTimeOffset(2026, 11, 16, 0, 0, 0, TimeSpan.Zero);
        var old = now.AddYears(-2);
        var folders = new List<LeftoverCandidate>
        {
            new(@"C:\AppData\JogoAntigo", "JogoAntigo", old),
            new(@"C:\AppData\Spotify", "Spotify", old),            // installed
            new(@"C:\AppData\EpicGamesLauncher", "EpicGamesLauncher", old), // matches publisher/name
            new(@"C:\AppData\Microsoft", "Microsoft", old),        // system
            new(@"C:\AppData\Recente", "Recente", now.AddDays(-10)),  // used recently
            new(@"C:\AppData\.cache", ".cache", old),
        };
        var installed = new List<InstalledProgram>
        {
            new("Spotify", "Spotify AB"),
            new("Epic Games Launcher", "Epic Games, Inc."),
        };

        var result = LeftoverFinder.Find(folders, installed, now);

        Assert.Equal(["JogoAntigo"], result.Select(r => r.Name));
    }

    [Theory]
    [InlineData("Epic Games, Inc.", "epicgamesinc")]
    [InlineData("Ação Café", "acaocafe")]
    [InlineData("7-Zip 23.01 (x64)", "7zip2301x64")]
    public void Normalize(string input, string expected)
    {
        Assert.Equal(expected, LeftoverFinder.Normalize(input));
    }
}
