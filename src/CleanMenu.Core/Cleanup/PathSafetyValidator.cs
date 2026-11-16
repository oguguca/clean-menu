namespace CleanMenu.Core.Cleanup;

/// <summary>Why a path was refused.</summary>
public enum PathRejection
{
    None,
    Empty,
    NotFullyQualified,
    DeviceOrNetworkPath,
    OutsideAllowedRoot,
    IsTheRootItself,
    ProtectedLocation,
    ProtectedName,
    UnsafeRoot,
}

/// <summary>
/// Last line of defense before any file is deleted. A path is accepted only when it is a local,
/// fully qualified path strictly inside the category root, outside every protected location and
/// not a known credential/session file. Everything else is refused.
/// </summary>
public sealed class PathSafetyValidator
{
    /// <summary>
    /// Folder names that hold logins, cookies or site data in browsers. Never touched, even if a
    /// cache root were misconfigured to include them.
    /// </summary>
    private static readonly HashSet<string> ProtectedSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "Network", "Local Storage", "Session Storage", "IndexedDB", "databases", "Sessions",
        "Extensions", "Local Extension Settings", "Sync Data", "storage",
    };

    /// <summary>Credential, cookie, history and settings files of browsers and the system.</summary>
    private static readonly HashSet<string> ProtectedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Cookies", "Cookies-journal", "Login Data", "Login Data-journal", "Login Data For Account",
        "Web Data", "Web Data-journal", "History", "Bookmarks", "Preferences", "Secure Preferences",
        "Local State", "logins.json", "logins-backup.json", "key3.db", "key4.db", "cert9.db",
        "cookies.sqlite", "places.sqlite", "formhistory.sqlite", "signons.sqlite", "sessionstore.jsonlz4",
        "NTUSER.DAT", "UsrClass.dat", "desktop.ini",
    };

    private static readonly HashSet<string> ProtectedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".kdbx", ".pfx", ".p12", ".pem", ".key", ".ost", ".pst", ".wallet",
    };

    private readonly IReadOnlyList<string> _protectedRoots;

    /// <param name="protectedRoots">
    /// Folders that must never be cleaned (Windows, Program Files, Documents, Desktop, Pictures…).
    /// </param>
    public PathSafetyValidator(IEnumerable<string> protectedRoots)
    {
        ArgumentNullException.ThrowIfNull(protectedRoots);
        _protectedRoots = protectedRoots
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(Normalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Checks that a category root is a reasonable cleanup location: absolute, local, at least three
    /// levels deep (never a drive root or a user profile root) and not a protected folder itself.
    /// </summary>
    public PathRejection ValidateRoot(string root)
    {
        if (BasicCheck(root) is var basic && basic != PathRejection.None)
        {
            return basic;
        }

        var full = Normalize(root);
        var depth = full.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).Length;
        if (depth < 3)
        {
            return PathRejection.UnsafeRoot;
        }

        foreach (var protectedRoot in _protectedRoots)
        {
            if (IsSameOrInside(full, protectedRoot) || IsSameOrInside(protectedRoot, full))
            {
                return PathRejection.ProtectedLocation;
            }
        }

        return PathRejection.None;
    }

    /// <summary>Checks a file path against the root of the category that found it.</summary>
    public PathRejection Validate(string path, string allowedRoot)
    {
        if (BasicCheck(path) is var basic && basic != PathRejection.None)
        {
            return basic;
        }

        if (ValidateRoot(allowedRoot) is var rootCheck && rootCheck != PathRejection.None)
        {
            return PathRejection.UnsafeRoot;
        }

        var full = Normalize(path);
        var root = Normalize(allowedRoot);

        if (string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
        {
            return PathRejection.IsTheRootItself;
        }

        if (!IsSameOrInside(full, root))
        {
            return PathRejection.OutsideAllowedRoot;
        }

        foreach (var protectedRoot in _protectedRoots)
        {
            if (IsSameOrInside(full, protectedRoot))
            {
                return PathRejection.ProtectedLocation;
            }
        }

        var relative = full[(root.Length + 1)..];
        var segments = relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Take(segments.Length - 1).Any(ProtectedSegments.Contains))
        {
            return PathRejection.ProtectedName;
        }

        var fileName = segments[^1];
        if (ProtectedFileNames.Contains(fileName) || ProtectedExtensions.Contains(Path.GetExtension(fileName)))
        {
            return PathRejection.ProtectedName;
        }

        return PathRejection.None;
    }

    private static PathRejection BasicCheck(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return PathRejection.Empty;
        }

        if (path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal))
        {
            // UNC shares and device namespaces (\\?\, \\.\) are never cleaned.
            return PathRejection.DeviceOrNetworkPath;
        }

        if (!Path.IsPathFullyQualified(path))
        {
            return PathRejection.NotFullyQualified;
        }

        return PathRejection.None;
    }

    private static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Replace('/', '\\')));

    private static bool IsSameOrInside(string path, string folder) =>
        string.Equals(path, folder, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    /// <summary>Plain-language reason, for logs and previews.</summary>
    public static string Describe(PathRejection rejection) => rejection switch
    {
        PathRejection.None => "Permitido",
        PathRejection.OutsideAllowedRoot => "Fora da pasta permitida",
        PathRejection.ProtectedLocation => "Pasta protegida",
        PathRejection.ProtectedName => "Arquivo protegido (senhas, cookies ou configurações)",
        PathRejection.DeviceOrNetworkPath => "Caminho de rede ou de dispositivo",
        PathRejection.UnsafeRoot => "Pasta de origem insegura",
        PathRejection.IsTheRootItself => "A própria pasta de origem",
        _ => "Caminho inválido",
    };
}
