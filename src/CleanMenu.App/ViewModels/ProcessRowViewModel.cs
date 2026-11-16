using CleanMenu.Core.Formatting;
using CleanMenu.Core.Processes;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CleanMenu.App.ViewModels;

/// <summary>One process in a list. Updated in place so the list does not jump while refreshing.</summary>
public sealed partial class ProcessRowViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CpuText))]
    private double _cpuPercent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MemoryText))]
    private long _memoryBytes;

    [ObservableProperty]
    private string? _windowTitle;

    public ProcessRowViewModel(ProcessUsage usage)
    {
        ArgumentNullException.ThrowIfNull(usage);
        Pid = usage.Pid;
        Name = usage.Name;
        Kind = usage.Kind;
        IsProtected = usage.IsProtected;
        Advice = usage.Advice;
        Update(usage);
    }

    public int Pid { get; }

    public string Name { get; }

    public ProcessKind Kind { get; }

    public bool IsProtected { get; }

    public string Advice { get; }

    /// <summary>Name the user recognizes: window title for apps, executable name otherwise.</summary>
    public string DisplayName => WindowTitle is { Length: > 0 } title && Kind == ProcessKind.UserApp ? $"{Name} — {title}" : Name;

    public string KindText => KindLabel(Kind);

    public static string KindLabel(ProcessKind kind) => kind switch
    {
        ProcessKind.CriticalSystem => "Windows (essencial)",
        ProcessKind.WindowsComponent => "Windows",
        ProcessKind.UserApp => "Programa aberto",
        _ => "Segundo plano",
    };

    public string CpuText => HealthDisplay.Percent(CpuPercent);

    public string MemoryText => ByteSize.Format(MemoryBytes);

    public void Update(ProcessUsage usage)
    {
        ArgumentNullException.ThrowIfNull(usage);
        CpuPercent = usage.CpuPercent;
        MemoryBytes = usage.MemoryBytes;
        WindowTitle = usage.WindowTitle;
        OnPropertyChanged(nameof(DisplayName));
    }
}
