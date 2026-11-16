using System.Globalization;
using CleanMenu.Core.Cleanup;
using CleanMenu.Core.Formatting;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CleanMenu.App.ViewModels;

/// <summary>One cleanup category with its checkbox and the numbers found by the analysis.</summary>
public sealed partial class CleanupCategoryRowViewModel : ObservableObject
{
    public const int PreviewLimit = 100;
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnalyzed), nameof(CountText), nameof(OccupiedText), nameof(RecoverableText),
        nameof(RecoverableBytes), nameof(RecoverableCount), nameof(PreviewFiles), nameof(PreviewMoreText), nameof(HasPreview), nameof(NoteText))]
    private CategoryAnalysis? _analysis;

    [ObservableProperty]
    private bool _isPreviewOpen;

    public CleanupCategoryRowViewModel(CleanupCategory category)
    {
        Category = category ?? throw new ArgumentNullException(nameof(category));
        _isSelected = category.SelectedByDefault && !category.RequiresAdmin;
    }

    public CleanupCategory Category { get; }

    public string Title => Category.Title;

    public string Description => Category.Description;

    public string Group => Category.Group;

    /// <summary>Admin-only categories are shown but cannot be selected in this version.</summary>
    public bool CanSelect => !Category.RequiresAdmin;

    public bool IsAnalyzed => Analysis is not null;

    public int RecoverableCount => Analysis?.EffectiveRecoverableCount ?? 0;

    public long RecoverableBytes => Analysis?.EffectiveRecoverableBytes ?? 0;

    public string CountText => Analysis is null
        ? "—"
        : Category.RequiresAdmin ? "—" : $"{RecoverableCount.ToString("N0", PtBr)} arquivos";

    public string OccupiedText => Analysis is null || Category.RequiresAdmin
        ? "—"
        : Category.Kind == CleanupKind.Files ? ByteSize.Format(Analysis.TotalBytes) : ByteSize.Format(Analysis.SpecialBytes);

    public string RecoverableText => Analysis is null || Category.RequiresAdmin ? "—" : ByteSize.Format(RecoverableBytes);

    public string? NoteText => Category.RequiresAdmin
        ? "Precisa de permissão de administrador. Disponível em uma próxima versão."
        : Analysis is { } analysis && analysis.Category.Kind == CleanupKind.Files && analysis.TotalFileCount > analysis.RecoverableCount
            ? $"{(analysis.TotalFileCount - analysis.RecoverableCount).ToString("N0", PtBr)} arquivos recentes ou protegidos serão mantidos. {Category.Hint}".Trim()
            : Category.Hint;

    public bool HasPreview => Analysis is { Files.Count: > 0 };

    /// <summary>Largest files that would be removed, for the "Ver arquivos" preview.</summary>
    public IReadOnlyList<PreviewFile> PreviewFiles => Analysis?.Files
        .OrderByDescending(f => f.SizeBytes)
        .Take(PreviewLimit)
        .Select(f => new PreviewFile(f.Path, ByteSize.Format(f.SizeBytes)))
        .ToList() ?? [];

    public string? PreviewMoreText => Analysis is { } a && a.Files.Count > PreviewLimit
        ? $"e mais {(a.Files.Count - PreviewLimit).ToString("N0", PtBr)} arquivos menores"
        : null;
}

public sealed record PreviewFile(string Path, string SizeText);
