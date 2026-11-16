using CleanMenu.Core.Cleanup;

namespace CleanMenu.Infrastructure.Cleanup;

/// <summary>
/// Cleanup categories for the current user. Every location is a cache or temporary folder that
/// programs recreate on their own. Browser categories only include cache folders: passwords,
/// cookies, history and open tabs live elsewhere and are never touched.
/// </summary>
public sealed class WindowsCleanupCatalog(string localAppData, string tempPath, string windowsDir) : ICleanupCatalog
{
    public const string GroupTemp = CleanupGroups.Temp;
    public const string GroupBrowsers = CleanupGroups.Browsers;
    public const string GroupRecycleBin = CleanupGroups.RecycleBin;
    public const string GroupOther = CleanupGroups.Other;

    private static readonly TimeSpan OneDay = TimeSpan.FromDays(1);
    private static readonly TimeSpan OneHour = TimeSpan.FromHours(1);

    public WindowsCleanupCatalog()
        : this(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Path.GetTempPath(),
            Environment.GetFolderPath(Environment.SpecialFolder.Windows))
    {
    }

    public IReadOnlyList<CleanupCategory> GetCategories()
    {
        var local = localAppData;
        var localLow = Path.Combine(Path.GetDirectoryName(local) ?? local, "LocalLow");

        var categories = new List<CleanupCategory>
        {
            new("user-temp", "Arquivos temporários do usuário",
                "Arquivos que os programas criam para uso rápido e esquecem de apagar. Só removemos os que têm mais de 1 dia.",
                GroupTemp, [new(Path.TrimEndingDirectorySeparator(tempPath))], OneDay, SelectedByDefault: true),

            new("windows-temp", "Arquivos temporários do Windows",
                "Arquivos temporários do próprio Windows e de instalações.",
                GroupTemp, [new(Path.Combine(windowsDir, "Temp"))], OneDay, SelectedByDefault: false, RequiresAdmin: true),

            new("thumbnails", "Miniaturas de imagens",
                "Cópias pequenas das suas fotos e vídeos usadas para mostrar as miniaturas nas pastas. O Windows cria de novo quando precisar.",
                GroupTemp, [new(Path.Combine(local, "Microsoft", "Windows", "Explorer"), "thumbcache_*.db", Recursive: false)],
                TimeSpan.Zero, SelectedByDefault: true, Hint: "Algumas miniaturas ficam em uso pelo Windows e são mantidas."),

            new("error-reports", "Relatórios de erros",
                "Relatórios guardados quando um programa travou. Já foram usados ou não serão mais enviados.",
                GroupTemp,
                [
                    new(Path.Combine(local, "Microsoft", "Windows", "WER", "ReportArchive")),
                    new(Path.Combine(local, "Microsoft", "Windows", "WER", "ReportQueue")),
                ], OneDay, SelectedByDefault: true),

            new("crash-dumps", "Arquivos de diagnóstico de travamentos",
                "Cópias da memória salvas quando um programa fechou sozinho. Só servem para técnicos investigarem o erro.",
                GroupTemp, [new(Path.Combine(local, "CrashDumps"), "*.dmp")], OneDay, SelectedByDefault: true),
        };

        categories.AddRange(BrowserCategories(local));

        categories.Add(new("recycle-bin", "Lixeira",
            "Arquivos que você já apagou e estão na Lixeira. Depois de esvaziar, não dá para recuperar.",
            GroupRecycleBin, [], TimeSpan.Zero, SelectedByDefault: false, Kind: CleanupKind.RecycleBin));

        categories.Add(new("shader-cache", "Cache de gráficos (shaders)",
            "Arquivos que a placa de vídeo guarda para abrir jogos e programas mais rápido. São recriados automaticamente; o primeiro uso depois pode ser um pouco mais lento.",
            GroupOther,
            [
                new(Path.Combine(local, "D3DSCache")),
                new(Path.Combine(local, "NVIDIA", "DXCache")),
                new(Path.Combine(local, "NVIDIA", "GLCache")),
                new(Path.Combine(local, "AMD", "DxCache")),
                new(Path.Combine(local, "AMD", "DxcCache")),
                new(Path.Combine(local, "AMD", "GLCache")),
                new(Path.Combine(localLow, "Intel", "ShaderCache")),
            ], OneHour, SelectedByDefault: false));

        categories.Add(new("windows-update", "Arquivos antigos de atualização do Windows",
            "Arquivos de atualizações que o Windows já instalou.",
            GroupOther, [new(Path.Combine(windowsDir, "SoftwareDistribution", "Download"))], OneDay,
            SelectedByDefault: false, RequiresAdmin: true));

        return categories;
    }

    private static IEnumerable<CleanupCategory> BrowserCategories(string local)
    {
        const string browserHint = "Feche o navegador antes de limpar para remover tudo. Senhas, cookies e histórico não são apagados.";

        var chromium = new (string Id, string Name, string UserData)[]
        {
            ("chrome", "Google Chrome", Path.Combine(local, "Google", "Chrome", "User Data")),
            ("edge", "Microsoft Edge", Path.Combine(local, "Microsoft", "Edge", "User Data")),
            ("brave", "Brave", Path.Combine(local, "BraveSoftware", "Brave-Browser", "User Data")),
            ("opera", "Opera", Path.Combine(local, "Opera Software", "Opera Stable")),
        };

        foreach (var (id, name, userData) in chromium)
        {
            if (!Directory.Exists(userData))
            {
                continue;
            }

            var targets = new List<CleanupTarget>
            {
                new(Path.Combine(userData, "ShaderCache")),
                new(Path.Combine(userData, "GrShaderCache")),
            };

            foreach (var profile in ChromiumProfiles(userData))
            {
                targets.Add(new(Path.Combine(profile, "Cache")));
                targets.Add(new(Path.Combine(profile, "Code Cache")));
                targets.Add(new(Path.Combine(profile, "GPUCache")));
            }

            yield return new CleanupCategory($"browser-{id}", $"Cache do {name}",
                "Cópias de páginas e imagens que o navegador guarda para abrir sites mais rápido. Ele baixa de novo quando precisar.",
                GroupBrowsers, targets, OneHour, SelectedByDefault: true, Hint: browserHint);
        }

        var firefoxProfiles = Path.Combine(local, "Mozilla", "Firefox", "Profiles");
        if (Directory.Exists(firefoxProfiles))
        {
            var targets = SafeDirectories(firefoxProfiles)
                .SelectMany(p => new CleanupTarget[] { new(Path.Combine(p, "cache2")), new(Path.Combine(p, "startupCache")) })
                .ToList();

            yield return new CleanupCategory("browser-firefox", "Cache do Firefox",
                "Cópias de páginas e imagens que o Firefox guarda para abrir sites mais rápido. Ele baixa de novo quando precisar.",
                GroupBrowsers, targets, OneHour, SelectedByDefault: true, Hint: browserHint);
        }
    }

    private static IEnumerable<string> ChromiumProfiles(string userData)
    {
        // Opera keeps its single profile directly in the user data folder.
        if (File.Exists(Path.Combine(userData, "Preferences")))
        {
            yield return userData;
        }

        foreach (var directory in SafeDirectories(userData))
        {
            var name = Path.GetFileName(directory);
            if (name.Equals("Default", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Guest Profile", StringComparison.OrdinalIgnoreCase))
            {
                yield return directory;
            }
        }
    }

    private static List<string> SafeDirectories(string path)
    {
        try
        {
            return Directory.EnumerateDirectories(path, "*", new EnumerationOptions
            {
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
            }).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
