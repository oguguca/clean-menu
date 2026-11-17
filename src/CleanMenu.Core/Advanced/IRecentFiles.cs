namespace CleanMenu.Core.Advanced;

/// <summary>Files Windows lists as recently opened by the user (Explorer "Recent items").</summary>
public interface IRecentFiles
{
    /// <summary>Full paths, compared ignoring case. Empty when the list cannot be read.</summary>
    IReadOnlySet<string> GetRecentlyOpened();
}
