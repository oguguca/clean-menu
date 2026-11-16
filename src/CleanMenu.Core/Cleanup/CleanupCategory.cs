using CleanMenu.Core.Platform;

namespace CleanMenu.Core.Cleanup;

/// <summary>A folder (and file pattern) a cleanup category may remove files from.</summary>
/// <param name="Root">Absolute folder. Files are only ever removed strictly inside it.</param>
/// <param name="SearchPattern">File name pattern, e.g. "*" or "thumbcache_*.db".</param>
/// <param name="Recursive">Whether subfolders are included.</param>
public sealed record CleanupTarget(string Root, string SearchPattern = "*", bool Recursive = true);

/// <summary>Group headings shown on the cleanup page.</summary>
public static class CleanupGroups
{
    public const string Temp = "Arquivos temporários";
    public const string Browsers = "Navegadores";
    public const string RecycleBin = "Lixeira";
    public const string Other = "Outros";
}

/// <summary>How a category is cleaned.</summary>
public enum CleanupKind
{
    /// <summary>Regular files under <see cref="CleanupCategory.Targets"/>.</summary>
    Files,

    /// <summary>The Windows Recycle Bin (emptied through the Windows API).</summary>
    RecycleBin,
}

/// <summary>
/// Something Clean Menu can clean, described for non-technical users.
/// </summary>
/// <param name="Id">Stable id (used in settings and history).</param>
/// <param name="Title">Plain name shown to the user.</param>
/// <param name="Description">One plain sentence: what it is and why it is safe to remove.</param>
/// <param name="Group">Group heading ("Arquivos temporários", "Navegadores"…).</param>
/// <param name="Targets">Folders to scan (empty for special kinds).</param>
/// <param name="MinimumAge">Files modified more recently than this are kept (they may still be in use).</param>
/// <param name="SelectedByDefault">Whether the checkbox starts checked.</param>
/// <param name="RequiresAdmin">Needs administrator rights; not cleaned from the normal app.</param>
/// <param name="Os">Windows versions where the category exists.</param>
/// <param name="Kind">How it is cleaned.</param>
/// <param name="Hint">Optional tip, e.g. "Feche o navegador para limpar tudo".</param>
public sealed record CleanupCategory(
    string Id,
    string Title,
    string Description,
    string Group,
    IReadOnlyList<CleanupTarget> Targets,
    TimeSpan MinimumAge,
    bool SelectedByDefault,
    bool RequiresAdmin = false,
    OsScope Os = OsScope.All,
    CleanupKind Kind = CleanupKind.Files,
    string? Hint = null);

/// <summary>Builds the list of categories for the current machine.</summary>
public interface ICleanupCatalog
{
    IReadOnlyList<CleanupCategory> GetCategories();
}
