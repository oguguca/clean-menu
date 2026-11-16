namespace CleanMenu.Core.Cleanup;

/// <summary>Size and item count of the Recycle Bin on all drives.</summary>
public sealed record RecycleBinInfo(long ItemCount, long SizeBytes);

/// <summary>The Windows Recycle Bin.</summary>
public interface IRecycleBin
{
    /// <summary>Returns null when Windows cannot report it.</summary>
    RecycleBinInfo? Query();

    /// <summary>Empties the Recycle Bin on all drives. Only called after the user confirms.</summary>
    bool Empty();
}
