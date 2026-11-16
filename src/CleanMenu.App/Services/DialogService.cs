using System.Windows;
using CleanMenu.App.Views;

namespace CleanMenu.App.Services;

/// <summary>Confirmation request shown before any action that changes the computer.</summary>
/// <param name="Title">Short question ("Limpar 1,2 GB?").</param>
/// <param name="Message">What will happen, in plain words.</param>
/// <param name="Details">Optional bullet points (categories, warnings).</param>
/// <param name="ConfirmText">Text of the confirming button ("Limpar agora").</param>
/// <param name="IsDestructive">Shows the confirm button in the warning color.</param>
public sealed record ConfirmationRequest(
    string Title,
    string Message,
    IReadOnlyList<string> Details,
    string ConfirmText,
    bool IsDestructive = true);

public interface IDialogService
{
    bool Confirm(ConfirmationRequest request);
}

public sealed class DialogService : IDialogService
{
    public bool Confirm(ConfirmationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var dialog = new ConfirmDialog(request) { Owner = Application.Current?.MainWindow };
        return dialog.ShowDialog() == true;
    }
}
