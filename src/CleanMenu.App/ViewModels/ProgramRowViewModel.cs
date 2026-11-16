using CleanMenu.Core.Formatting;
using CleanMenu.Core.Processes;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CleanMenu.App.ViewModels;

/// <summary>A program (all its processes together) in the memory list. Updated in place.</summary>
public sealed partial class ProgramRowViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private int _processCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MemoryText))]
    private long _memoryBytes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KindText), nameof(Advice))]
    private ProcessKind _kind;

    public ProgramRowViewModel(ProgramUsage usage)
    {
        ArgumentNullException.ThrowIfNull(usage);
        Name = usage.Name;
        Update(usage);
    }

    public string Name { get; }

    public string DisplayName => ProcessCount > 1 ? $"{Name} ({ProcessCount} partes)" : Name;

    public string MemoryText => ByteSize.Format(MemoryBytes);

    public string Advice => ProcessClassifier.Advice(Kind);

    public string KindText => ProcessRowViewModel.KindLabel(Kind);

    public void Update(ProgramUsage usage)
    {
        ArgumentNullException.ThrowIfNull(usage);
        ProcessCount = usage.ProcessCount;
        MemoryBytes = usage.MemoryBytes;
        Kind = usage.Kind;
    }
}
