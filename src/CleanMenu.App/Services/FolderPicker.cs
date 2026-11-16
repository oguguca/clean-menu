using Microsoft.Win32;

namespace CleanMenu.App.Services;

public interface IFolderPicker
{
    /// <summary>Returns the chosen folder, or null when the user cancels.</summary>
    string? Pick(string initialFolder);
}

public sealed class FolderPicker : IFolderPicker
{
    public string? Pick(string initialFolder)
    {
        var dialog = new OpenFolderDialog { Title = "Escolha a pasta para analisar", InitialDirectory = initialFolder };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
