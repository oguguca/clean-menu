using CleanMenu.Core.Advanced;
using CleanMenu.Core.Cleanup;
using CleanMenu.Core.Tests.Cleanup;

namespace CleanMenu.Core.Tests.Advanced;

/// <summary>Files the user uses often must never be offered for removal.</summary>
public class RecentUseTests
{
    private const string Root = @"C:\Users\ana";
    private const long Mb = 1024L * 1024;
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LongAgo = Now.AddDays(-400);

    private sealed class SameHash : IFileHasher
    {
        public List<string> Hashed { get; } = [];

        public Task<string?> Sha256Async(string path, CancellationToken cancellationToken)
        {
            Hashed.Add(path);
            return Task.FromResult<string?>("SAME");
        }
    }

    private sealed class RecentList(params string[] paths) : IRecentFiles
    {
        public IReadOnlySet<string> GetRecentlyOpened() => new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase);
    }

    private sealed class BrokenRecentList : IRecentFiles
    {
        public IReadOnlySet<string> GetRecentlyOpened() => throw new IOException("unreadable");
    }

    private static AdvancedScanner Scanner(FakeCleanupFileSystem fs, IFileHasher? hasher = null, IRecentFiles? recent = null) =>
        new(fs, hasher ?? new SameHash(), recent, new ManualClock(Now));

    private static CleanupFile File(string path, DateTimeOffset write, DateTimeOffset? access = null, DateTimeOffset? created = null, long size = 500 * Mb) =>
        new(path, size, write, FileAttributes.Normal, access, created);

    [Fact]
    public void LastUsed_IsTheLatestOfWriteAccessAndCreation()
    {
        Assert.Equal(LongAgo, File("x", LongAgo).LastUsedUtc);
        Assert.Equal(Now, File("x", LongAgo, access: Now).LastUsedUtc);
        Assert.Equal(Now, File("x", LongAgo, access: LongAgo, created: Now).LastUsedUtc);
    }

    [Fact]
    public void RecentWindow_IsThreeMonths()
    {
        Assert.True(AdvancedFileRules.IsRecentlyUsed(Now.AddDays(-89), Now));
        Assert.False(AdvancedFileRules.IsRecentlyUsed(Now.AddDays(-91), Now));
    }

    [Fact]
    public void LargeFiles_SkipAnythingOpenedChangedOrCopiedRecently()
    {
        var fs = new FakeCleanupFileSystem();
        fs.Put(File(Root + @"\Videos\esquecido.mp4", LongAgo, access: LongAgo, created: LongAgo));
        fs.Put(File(Root + @"\Videos\assistido-ontem.mp4", LongAgo, access: Now.AddDays(-1)));
        fs.Put(File(Root + @"\Videos\editado.mp4", Now.AddDays(-10)));
        fs.Put(File(Root + @"\Videos\copiado.mp4", LongAgo, created: Now.AddDays(-5)));
        fs.Put(File(Root + @"\Videos\recente-no-windows.mp4", LongAgo));

        var result = Scanner(fs, recent: new RecentList(Root + @"\Videos\RECENTE-no-windows.mp4")).FindLargeFiles(Root, []);

        Assert.Equal([Root + @"\Videos\esquecido.mp4"], result.Select(f => f.Path));
    }

    [Fact]
    public async Task Duplicates_NeverIncludeRecentlyUsedCopies_AndNeverReadThem()
    {
        var fs = new FakeCleanupFileSystem();
        fs.Put(File(Root + @"\a\foto.jpg", LongAgo));
        fs.Put(File(Root + @"\b\foto.jpg", LongAgo));
        fs.Put(File(Root + @"\c\foto.jpg", LongAgo, access: Now.AddDays(-2)));
        var hasher = new SameHash();

        var groups = await Scanner(fs, hasher).FindDuplicatesAsync(Root, []);

        var group = Assert.Single(groups);
        Assert.Equal(2, group.Files.Count);
        Assert.DoesNotContain(Root + @"\c\foto.jpg", hasher.Hashed);
    }

    [Fact]
    public void UnreadableRecentList_StillAppliesTheAgeRule()
    {
        var fs = new FakeCleanupFileSystem();
        fs.Put(File(Root + @"\v\velho.mp4", LongAgo));
        fs.Put(File(Root + @"\v\novo.mp4", Now.AddDays(-1)));

        var result = Scanner(fs, recent: new BrokenRecentList()).FindLargeFiles(Root, []);

        Assert.Equal([Root + @"\v\velho.mp4"], result.Select(f => f.Path));
    }

    [Fact]
    public void CanStillRecycle_RefusesFilesChangedOrUsedAfterTheScan()
    {
        var fs = new FakeCleanupFileSystem();
        var scanned = File(Root + @"\v\filme.mp4", LongAgo, access: LongAgo);
        fs.Put(scanned);
        var scanner = Scanner(fs);
        Assert.True(scanner.CanStillRecycle(scanned));

        // Hashing during the scan updates the access date: that alone must not block removal.
        fs.Put(scanned with { LastAccessUtc = Now });
        Assert.True(scanner.CanStillRecycle(scanned));

        fs.Put(scanned with { LastWriteUtc = Now.AddDays(-1) });
        Assert.False(scanner.CanStillRecycle(scanned));

        fs.Put(scanned with { CreationUtc = Now.AddDays(-1) });
        Assert.False(scanner.CanStillRecycle(scanned));

        Assert.False(Scanner(fs, recent: new RecentList(scanned.Path)).CanStillRecycle(scanned with { CreationUtc = null }));
        Assert.False(Scanner(new FakeCleanupFileSystem()).CanStillRecycle(scanned)); // gone
    }
}
