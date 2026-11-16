using CleanMenu.Core.Settings;
using CleanMenu.Infrastructure.Settings;

namespace CleanMenu.Infrastructure.Tests.Settings;

/// <summary>Uses a sandbox folder under %TEMP%; never the real settings file.</summary>
public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "CleanMenuTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void MissingFile_GivesDefaults()
    {
        var settings = new JsonSettingsStore(_folder).Load();

        Assert.Equal(new AppSettings(), settings);
    }

    [Fact]
    public void SaveAndLoad_RoundTrip()
    {
        var store = new JsonSettingsStore(_folder);
        var saved = new AppSettings
        {
            Theme = ThemePreference.Light,
            Os = OsPreference.Windows10,
            RefreshSeconds = 5,
            AnalyzeOnStartup = true,
            ConfirmBeforeCleaning = false,
            DefaultCleanupCategories = ["user-temp", "recycle-bin"],
        };

        store.Save(saved);
        var loaded = store.Load();

        Assert.Equal(ThemePreference.Light, loaded.Theme);
        Assert.Equal(OsPreference.Windows10, loaded.Os);
        Assert.Equal(5, loaded.RefreshSeconds);
        Assert.True(loaded.AnalyzeOnStartup);
        Assert.False(loaded.ConfirmBeforeCleaning);
        Assert.Equal(["user-temp", "recycle-bin"], loaded.DefaultCleanupCategories);
        Assert.Contains("\"Light\"", File.ReadAllText(Path.Combine(_folder, "settings.json")), StringComparison.Ordinal);
    }

    [Fact]
    public void DamagedFile_GivesDefaults()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "settings.json"), "{ isto não é json");

        Assert.Equal(new AppSettings(), new JsonSettingsStore(_folder).Load());
    }
}
