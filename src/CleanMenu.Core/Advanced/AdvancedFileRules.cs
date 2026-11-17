namespace CleanMenu.Core.Advanced;

/// <summary>
/// Decides which files Limpeza Avançada may ever show for removal. Only the user's own content
/// qualifies: never anything inside program, app-data, development or tool folders, and never
/// program files (executables, libraries, scripts). Two identical files inside a program are not
/// "duplicates" — the program needs both.
/// </summary>
public static class AdvancedFileRules
{
    /// <summary>Folder names that always belong to programs, tools or projects (any depth).</summary>
    private static readonly HashSet<string> BlockedFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "AppData", "Program Files", "Program Files (x86)", "ProgramData", "Windows", "WindowsApps",
        "node_modules", "bower_components", "vendor", "packages", "site-packages", "dist-packages", "Lib", "Scripts",
        "venv", "env", "__pycache__", "bin", "obj", "build", "dist", "out", "target", "Debug", "Release",
        "android", "ios", "gradle", "Pods", "DerivedData",
        "Steam", "steamapps", "Epic Games", "Riot Games", "Battle.net", "GOG Galaxy", "Origin Games", "EA Games", "Ubisoft",
        "Android", "Sdk", "Programs", "OneDriveTemp",
    };

    /// <summary>Content types a person creates or collects. Only these can be reported as duplicates.</summary>
    private static readonly HashSet<string> UserContentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // images
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tif", ".tiff", ".webp", ".heic", ".heif", ".raw", ".cr2", ".nef", ".arw", ".dng", ".psd",
        // video
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".m4v", ".webm", ".mpg", ".mpeg", ".3gp",
        // audio
        ".mp3", ".wav", ".flac", ".aac", ".m4a", ".ogg", ".wma", ".opus",
        // documents
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".odt", ".ods", ".odp", ".rtf", ".txt", ".epub",
        // archives and disk images
        ".zip", ".rar", ".7z", ".tar", ".gz", ".iso",
    };

    /// <summary>Files that make programs work. Never offered for removal, even when large.</summary>
    private static readonly HashSet<string> ProgramExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".sys", ".drv", ".ocx", ".cpl", ".msi", ".msix", ".appx", ".node", ".pak", ".asar", ".dat", ".bin",
        ".so", ".dylib", ".jar", ".pyd", ".pyc", ".wasm", ".js", ".mjs", ".cjs", ".ts", ".json", ".lib", ".a", ".obj", ".pdb",
        ".winmd", ".mui", ".db", ".sqlite", ".ldb", ".log", ".ini", ".cfg", ".vhd", ".vhdx", ".bundle", ".cab",
        ".bat", ".cmd", ".ps1", ".vbs", ".lnk", ".url", ".sav",
    };

    /// <summary>True when the path is inside a folder that belongs to a program, tool or project.</summary>
    public static bool IsInProgramArea(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var directory = Path.GetDirectoryName(path);
        if (directory is null)
        {
            return true;
        }

        foreach (var segment in directory.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            // Hidden tool folders such as .vscode, .gradle, .nuget, .git, .venv, .cache.
            if (segment.StartsWith('.') || BlockedFolderNames.Contains(segment))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>May this file appear in "Arquivos grandes"?</summary>
    public static bool CanOfferLargeFile(string path) =>
        !IsInProgramArea(path) && !ProgramExtensions.Contains(Path.GetExtension(path));

    /// <summary>May this file appear in "Duplicados"? Only user content (photos, videos, music, documents, archives).</summary>
    public static bool CanOfferDuplicate(string path) =>
        !IsInProgramArea(path) && UserContentExtensions.Contains(Path.GetExtension(path));

    /// <summary>Final check right before sending to the Recycle Bin.</summary>
    public static bool CanRecycle(string path) => CanOfferLargeFile(path);
}
