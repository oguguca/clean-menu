namespace CleanMenu.Core.Advanced;

public enum RecycleOutcome
{
    Recycled,
    NotFound,
    InUse,
    Refused,
    Cancelled,
}

/// <summary>
/// Sends user-chosen files to the Recycle Bin (never a permanent delete), so the user can restore
/// them. Used by Limpeza Avançada, where files are personal and chosen one by one.
/// </summary>
public interface IRecycler
{
    RecycleOutcome SendToRecycleBin(string path);
}
