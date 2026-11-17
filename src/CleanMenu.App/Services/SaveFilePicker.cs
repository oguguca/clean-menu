using Microsoft.Win32;

namespace CleanMenu.App.Services;

public interface ISaveFilePicker
{
    /// <summary>Asks where to save; Windows asks before replacing an existing file. Null when cancelled.</summary>
    string? Pick(string initialFolder, string suggestedName);
}

public sealed class SaveFilePicker : ISaveFilePicker
{
    public string? Pick(string initialFolder, string suggestedName)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Onde salvar o arquivo baixado?",
            InitialDirectory = initialFolder,
            FileName = suggestedName,
            OverwritePrompt = true,
            AddExtension = false,
            Filter = "Todos os arquivos (*.*)|*.*",
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
