using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using CleanMenu.App.Navigation;
using CleanMenu.App.Services;
using CleanMenu.Core.Advanced;
using CleanMenu.Core.Cleanup;
using CleanMenu.Core.Formatting;
using CleanMenu.Core.History;
using CleanMenu.Core.Processes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CleanMenu.App.ViewModels;

public enum AdvancedMode
{
    LargeFiles,
    Duplicates,
    Folders,
    Leftovers,
}

/// <summary>A file that the user may choose to send to the Recycle Bin.</summary>
public sealed partial class SelectableFileViewModel(CleanupFile file) : ObservableObject
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public CleanupFile File { get; } = file;

    public string Path => File.Path;

    public string Name => System.IO.Path.GetFileName(File.Path);

    public string Folder => System.IO.Path.GetDirectoryName(File.Path) ?? string.Empty;

    public string SizeText => ByteSize.Format(File.SizeBytes);

    public string DateText => $"Modificado em {File.LastWriteUtc.ToLocalTime().ToString("dd/MM/yyyy", PtBr)}";

    [ObservableProperty]
    private bool _isChecked;
}

/// <summary>A set of identical files.</summary>
public sealed class DuplicateGroupViewModel(DuplicateGroup group)
{
    public DuplicateGroup Group { get; } = group;

    public string Title => $"{Group.Files.Count} cópias iguais de {ByteSize.Format(Group.SizeBytes)} · manter só uma libera {ByteSize.Format(Group.WastedBytes)}";

    public ObservableCollection<SelectableFileViewModel> Files { get; } = new(group.Files.Select(f => new SelectableFileViewModel(f)));
}

/// <summary>A folder with its size, or a possible leftover of an uninstalled program.</summary>
public sealed record FolderRowViewModel(string Path, string Title, string Detail, double Percent);

/// <summary>
/// Limpeza Avançada: large files, duplicates (size + SHA-256), biggest folders and possible
/// leftovers of uninstalled programs. Nothing is deleted automatically; chosen files go to the
/// Recycle Bin after confirmation, so they can be restored.
/// </summary>
public sealed partial class AdvancedCleanupViewModel : PageViewModel, IDisposable
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private readonly AdvancedScanner _scanner;
    private readonly ILeftoverSource _leftovers;
    private readonly IRecycler _recycler;
    private readonly IFolderPicker _folderPicker;
    private readonly IProcessActions _processActions;
    private readonly IDialogService _dialogs;
    private readonly IOperationHistory _history;
    private readonly ISystemActions _systemActions;
    private readonly IReadOnlyList<string> _protectedFolders;
    private CancellationTokenSource? _cancellation;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModeHint), nameof(ShowsFiles), nameof(ShowsDuplicates), nameof(ShowsFolders))]
    private AdvancedMode _mode = AdvancedMode.LargeFiles;

    [ObservableProperty]
    private string _scanRoot;

    [ObservableProperty]
    private bool _allowDownloads;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand), nameof(CancelCommand), nameof(RecycleSelectedCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private double _progressPercent = double.NaN;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private string _selectionSummary = "Nada selecionado.";

    public AdvancedCleanupViewModel(
        AdvancedScanner scanner,
        ILeftoverSource leftovers,
        IRecycler recycler,
        IFolderPicker folderPicker,
        IProcessActions processActions,
        IDialogService dialogs,
        IOperationHistory history,
        ISystemActions systemActions,
        IReadOnlyList<string> protectedFolders,
        string defaultRoot,
        string? downloadsFolder = null)
        : base(PageCatalog.Get(AppPage.AdvancedCleanup))
    {
        _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
        _leftovers = leftovers ?? throw new ArgumentNullException(nameof(leftovers));
        _recycler = recycler ?? throw new ArgumentNullException(nameof(recycler));
        _folderPicker = folderPicker ?? throw new ArgumentNullException(nameof(folderPicker));
        _processActions = processActions ?? throw new ArgumentNullException(nameof(processActions));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _systemActions = systemActions ?? throw new ArgumentNullException(nameof(systemActions));
        _protectedFolders = protectedFolders ?? throw new ArgumentNullException(nameof(protectedFolders));
        _scanRoot = defaultRoot;
        DownloadsFolder = downloadsFolder ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }

    public ObservableCollection<SelectableFileViewModel> Files { get; } = [];

    public ObservableCollection<DuplicateGroupViewModel> Duplicates { get; } = [];

    public ObservableCollection<FolderRowViewModel> Folders { get; } = [];

    public bool ShowsFiles => Mode == AdvancedMode.LargeFiles;

    public bool ShowsDuplicates => Mode == AdvancedMode.Duplicates;

    public bool ShowsFolders => Mode is AdvancedMode.Folders or AdvancedMode.Leftovers;

    public string ModeHint => Mode switch
    {
        AdvancedMode.LargeFiles => "Arquivos com mais de 100 MB. Marque os que você não quer mais: eles vão para a Lixeira.",
        AdvancedMode.Duplicates => "Arquivos idênticos (mesmo tamanho e mesmo conteúdo, conferido com SHA-256). Mantenha pelo menos uma cópia de cada.",
        AdvancedMode.Folders => "As pastas que mais ocupam espaço dentro da pasta escolhida. Clique para abrir e decidir o que fazer.",
        _ => "Pastas de programas que parecem não estar mais instalados e não são usadas há mais de 6 meses. São só sugestões: confira antes de apagar qualquer coisa.",
    };

    public string DownloadsFolder { get; }

    private IEnumerable<SelectableFileViewModel> AllSelectable => Files.Concat(Duplicates.SelectMany(d => d.Files));

    private bool CanScan() => !IsBusy;

    private bool CanCancel() => IsBusy;

    private bool CanRecycle() => !IsBusy && AllSelectable.Any(f => f.IsChecked);

    public void Dispose() => _cancellation?.Dispose();

    [RelayCommand]
    private void ChooseFolder()
    {
        if (_folderPicker.Pick(ScanRoot) is { } folder)
        {
            ScanRoot = folder;
        }
    }

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync()
    {
        Message = null;
        if (_protectedFolders.Any(p => IsSameOrInside(ScanRoot, p)))
        {
            Message = "Esta pasta é do Windows ou de programas instalados e não pode ser analisada aqui.";
            return;
        }

        IsBusy = true;
        ProgressText = "Analisando… isso pode levar alguns minutos em pastas grandes.";
        ProgressPercent = double.NaN;
        _cancellation = new CancellationTokenSource();
        var token = _cancellation.Token;
        var excluded = _protectedFolders.Concat(AllowDownloads ? [] : new[] { DownloadsFolder }).ToList();
        var progress = new Progress<OperationProgress>(p =>
        {
            ProgressText = p.Total is { } total ? $"{p.Step} ({p.Processed.ToString("N0", PtBr)} de {total.ToString("N0", PtBr)})" : p.Step;
            ProgressPercent = p.Fraction is { } f ? f * 100 : double.NaN;
        });

        try
        {
            ClearResults();
            switch (Mode)
            {
                case AdvancedMode.LargeFiles:
                    var large = await Task.Run(() => _scanner.FindLargeFiles(ScanRoot, excluded, cancellationToken: token), token).ConfigureAwait(true);
                    AddFiles(large);
                    Message = large.Count == 0 ? "Nenhum arquivo grande encontrado nesta pasta." : null;
                    break;

                case AdvancedMode.Duplicates:
                    var groups = await Task.Run(() => _scanner.FindDuplicatesAsync(ScanRoot, excluded, progress: progress, cancellationToken: token), token).ConfigureAwait(true);
                    foreach (var group in groups.Take(300))
                    {
                        var vm = new DuplicateGroupViewModel(group);
                        foreach (var file in vm.Files)
                        {
                            file.PropertyChanged += OnFileChanged;
                        }

                        Duplicates.Add(vm);
                    }

                    Message = groups.Count == 0 ? "Nenhum arquivo duplicado encontrado." : $"Arquivos repetidos ocupam {ByteSize.Format(groups.Sum(g => g.WastedBytes))} a mais.";
                    break;

                case AdvancedMode.Folders:
                    var folders = await Task.Run(() => _scanner.FindLargestFolders(ScanRoot, excluded, token), token).ConfigureAwait(true);
                    var total = Math.Max(1, folders.Sum(f => f.SizeBytes));
                    foreach (var folder in folders.Take(50))
                    {
                        var title = string.Equals(folder.Path, ScanRoot, StringComparison.OrdinalIgnoreCase) ? "(arquivos soltos nesta pasta)" : Path.GetFileName(folder.Path);
                        Folders.Add(new FolderRowViewModel(folder.Path, title,
                            $"{ByteSize.Format(folder.SizeBytes)} · {folder.FileCount.ToString("N0", PtBr)} arquivos", 100.0 * folder.SizeBytes / total));
                    }

                    break;

                case AdvancedMode.Leftovers:
                    var leftovers = await Task.Run(() => LeftoverFinder.Find(_leftovers.GetAppDataFolders(), _leftovers.GetInstalledPrograms(), DateTimeOffset.UtcNow), token).ConfigureAwait(true);
                    foreach (var leftover in leftovers)
                    {
                        Folders.Add(new FolderRowViewModel(leftover.Path, leftover.Name,
                            $"Sem uso desde {leftover.LastWriteUtc.ToLocalTime().ToString("dd/MM/yyyy", PtBr)} · {leftover.Path}", double.NaN));
                    }

                    Message = leftovers.Count == 0 ? "Nenhuma sobra de programa encontrada." : null;
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            Message = "Análise cancelada.";
        }
        finally
        {
            _cancellation.Dispose();
            _cancellation = null;
            IsBusy = false;
            ProgressText = string.Empty;
            UpdateSelection();
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _cancellation?.Cancel();

    [RelayCommand]
    private void MarkDuplicateCopies()
    {
        foreach (var group in Duplicates)
        {
            for (var i = 0; i < group.Files.Count; i++)
            {
                // Keep the oldest copy (first), mark the others.
                group.Files[i].IsChecked = i > 0;
            }
        }
    }

    [RelayCommand]
    private void OpenFolder(string? path)
    {
        if (path is not null)
        {
            _systemActions.OpenTool(path);
        }
    }

    [RelayCommand]
    private void ShowInFolder(SelectableFileViewModel? file)
    {
        if (file is not null)
        {
            _processActions.OpenFileLocation(file.Path);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRecycle))]
    private async Task RecycleSelectedAsync()
    {
        var selected = AllSelectable.Where(f => f.IsChecked).ToList();
        if (selected.Count == 0)
        {
            return;
        }

        var unprotectedGroups = Duplicates.Where(d => d.Files.All(f => f.IsChecked)).ToList();
        var details = new List<string>
        {
            "Os arquivos vão para a Lixeira. Você pode restaurá-los de lá se mudar de ideia.",
            "Arquivos muito grandes podem não caber na Lixeira; nesse caso o Windows avisa antes.",
        };
        if (unprotectedGroups.Count > 0)
        {
            details.Insert(0, $"Atenção: em {unprotectedGroups.Count} grupo(s) de duplicados você marcou TODAS as cópias.");
        }

        if (!_dialogs.Confirm(new ConfirmationRequest(
                $"Mandar {selected.Count} arquivo(s) para a Lixeira?",
                $"Total: {ByteSize.Format(selected.Sum(f => f.File.SizeBytes))}.",
                details,
                "Mandar para a Lixeira")))
        {
            return;
        }

        var recycled = 0;
        long bytes = 0;
        var failed = 0;
        foreach (var file in selected)
        {
            if (_recycler.SendToRecycleBin(file.Path) == RecycleOutcome.Recycled)
            {
                recycled++;
                bytes += file.File.SizeBytes;
                file.IsChecked = false;
                Files.Remove(file);
                foreach (var group in Duplicates)
                {
                    group.Files.Remove(file);
                }
            }
            else
            {
                failed++;
            }
        }

        foreach (var group in Duplicates.Where(d => d.Files.Count < 2).ToList())
        {
            Duplicates.Remove(group);
        }

        Message = $"{recycled} arquivo(s) foram para a Lixeira ({ByteSize.Format(bytes)})." +
                  (failed > 0 ? $" {failed} não puderam ser movidos (em uso ou protegidos)." : string.Empty);
        await _history.AddAsync(new OperationRecord(Guid.NewGuid(), DateTimeOffset.Now, "Limpeza avançada (Lixeira)", [ModeTitle()],
            recycled, bytes, failed, [], failed > 0 ? "Concluída com avisos" : "Concluída", false)).ConfigureAwait(true);
        UpdateSelection();
    }

    private string ModeTitle() => Mode switch
    {
        AdvancedMode.LargeFiles => "Arquivos grandes",
        AdvancedMode.Duplicates => "Arquivos duplicados",
        _ => "Pastas",
    };

    partial void OnModeChanged(AdvancedMode value)
    {
        ClearResults();
        Message = null;
    }

    private void AddFiles(IEnumerable<CleanupFile> files)
    {
        foreach (var file in files)
        {
            var vm = new SelectableFileViewModel(file);
            vm.PropertyChanged += OnFileChanged;
            Files.Add(vm);
        }
    }

    private void ClearResults()
    {
        Files.Clear();
        Duplicates.Clear();
        Folders.Clear();
        UpdateSelection();
    }

    private void OnFileChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SelectableFileViewModel.IsChecked))
        {
            UpdateSelection();
        }
    }

    private void UpdateSelection()
    {
        var selected = AllSelectable.Where(f => f.IsChecked).ToList();
        SelectionSummary = selected.Count == 0
            ? "Nada selecionado."
            : $"Selecionado: {selected.Count} arquivo(s), {ByteSize.Format(selected.Sum(f => f.File.SizeBytes))}";
        RecycleSelectedCommand.NotifyCanExecuteChanged();
    }

    private static bool IsSameOrInside(string path, string folder)
    {
        var p = Path.TrimEndingDirectorySeparator(path);
        var f = Path.TrimEndingDirectorySeparator(folder);
        return p.Equals(f, StringComparison.OrdinalIgnoreCase) || p.StartsWith(f + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
