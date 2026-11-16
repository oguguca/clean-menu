using System.IO;
using System.Collections.ObjectModel;
using CleanMenu.App.Navigation;
using CleanMenu.App.Services;
using CleanMenu.Core.History;
using CleanMenu.Core.Processes;
using CleanMenu.Core.Startup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CleanMenu.App.ViewModels;

/// <summary>One program that starts with Windows.</summary>
public sealed partial class StartupRowViewModel(StartupEntry entry) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private StartupEntry _entry = entry;

    public string Name => Entry.Name;

    public string PublisherText => Entry.Publisher ?? "Editor não identificado";

    public string SourceText => Entry.SourceText;

    public string PathText => Entry.ExecutablePath ?? Entry.Command;

    public bool IsEnabled => Entry.IsEnabled;

    public bool CanToggle => Entry.CanToggle;

    public bool HasFile => Entry.ExecutablePath is { Length: > 0 } path && File.Exists(path);

    public string StatusText => Entry.IsEnabled ? "Abre ao ligar" : "Desligado";

    public string ToggleText => Entry.IsEnabled ? "Desligar" : "Religar";

    public string? LockText => Entry.CanToggle
        ? null
        : Entry.Source == StartupSource.ScheduledTask
            ? "Tarefa agendada: para mudar, use o Agendador de Tarefas do Windows."
            : "Vale para todos os usuários: precisa de permissão de administrador para mudar.";

    internal void Replace(StartupEntry updated)
    {
        Entry = updated;
        OnPropertyChanged(nameof(IsEnabled));
        OnPropertyChanged(nameof(ToggleText));
    }
}

/// <summary>
/// Inicialização page: what starts with Windows, with a reversible on/off switch (the same mechanism
/// Windows uses). Nothing is ever deleted.
/// </summary>
public sealed partial class StartupViewModel : PageViewModel
{
    private readonly IStartupManager _startup;
    private readonly IProcessActions _processActions;
    private readonly IDialogService _dialogs;
    private readonly IOperationHistory _history;
    private readonly IUiDispatcher _dispatcher;

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private string _summary = "Lendo os programas…";

    [ObservableProperty]
    private string? _message;

    public StartupViewModel(IStartupManager startup, IProcessActions processActions, IDialogService dialogs, IOperationHistory history, IUiDispatcher dispatcher)
        : base(PageCatalog.Get(AppPage.Startup))
    {
        _startup = startup ?? throw new ArgumentNullException(nameof(startup));
        _processActions = processActions ?? throw new ArgumentNullException(nameof(processActions));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _ = RefreshAsync();
    }

    public ObservableCollection<StartupRowViewModel> Rows { get; } = [];

    [RelayCommand]
    internal async Task RefreshAsync()
    {
        IsLoading = true;
        IReadOnlyList<StartupEntry> entries;
        try
        {
            entries = await _startup.GetEntriesAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            entries = [];
            _dispatcher.Post(() => Message = "Não foi possível ler a lista de inicialização agora.");
        }

        _dispatcher.Post(() =>
        {
            Rows.Clear();
            foreach (var entry in entries.OrderByDescending(e => e.IsEnabled).ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                Rows.Add(new StartupRowViewModel(entry));
            }

            UpdateSummary();
            IsLoading = false;
        });
    }

    [RelayCommand]
    private async Task ToggleAsync(StartupRowViewModel? row)
    {
        if (row is null || !row.CanToggle)
        {
            return;
        }

        var enable = !row.IsEnabled;
        if (!enable && !_dialogs.Confirm(new ConfirmationRequest(
                $"Desligar \"{row.Name}\" ao ligar o computador?",
                "O programa continua instalado e funcionando. Ele só não vai mais abrir sozinho quando o Windows iniciar.",
                ["Você pode religar a qualquer momento por aqui ou pelas Configurações do Windows.", $"Origem: {row.SourceText}"],
                "Desligar", IsDestructive: false)))
        {
            return;
        }

        try
        {
            _startup.SetEnabled(row.Entry, enable);
            row.Replace(row.Entry with { IsEnabled = enable });
            Message = enable ? $"\"{row.Name}\" vai voltar a abrir com o Windows." : $"\"{row.Name}\" não vai mais abrir com o Windows.";
            await _history.RecordActionAsync(enable ? "Inicialização: religado" : "Inicialização: desligado", row.Name, succeeded: true).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException or InvalidOperationException)
        {
            Message = $"Não foi possível alterar \"{row.Name}\": {ex.Message}";
            await _history.RecordActionAsync("Inicialização", row.Name, succeeded: false, ex.Message).ConfigureAwait(true);
        }

        UpdateSummary();
    }

    [RelayCommand]
    private void OpenLocation(StartupRowViewModel? row)
    {
        if (row?.Entry.ExecutablePath is { } path && !_processActions.OpenFileLocation(path))
        {
            Message = "Não foi possível abrir a pasta deste programa.";
        }
    }

    private void UpdateSummary()
    {
        var enabled = Rows.Count(r => r.IsEnabled);
        Summary = Rows.Count == 0
            ? "Nenhum programa encontrado na inicialização."
            : $"{enabled} programa(s) abrem sozinhos quando o computador liga · {Rows.Count - enabled} desligado(s)";
    }
}
