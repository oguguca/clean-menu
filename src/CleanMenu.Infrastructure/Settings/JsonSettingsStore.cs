using System.Text.Json;
using System.Text.Json.Serialization;
using CleanMenu.Core.Settings;

namespace CleanMenu.Infrastructure.Settings;

/// <summary>Settings in %LOCALAPPDATA%\CleanMenu\settings.json. A missing or damaged file means defaults.</summary>
public sealed class JsonSettingsStore(string dataFolder) : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public JsonSettingsStore()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CleanMenu"))
    {
    }

    public string DataFolder { get; } = dataFolder;

    private string FilePath => Path.Combine(DataFolder, "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new AppSettings();
            }

            return (JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options) ?? new AppSettings()).Sanitize();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Directory.CreateDirectory(DataFolder);

        // Write to a temporary file first so a crash never leaves a half-written settings file.
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings.Sanitize(), Options));
        File.Move(temp, FilePath, overwrite: true);
    }
}
