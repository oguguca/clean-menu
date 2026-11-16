using System.Windows;
using CleanMenu.Core.Platform;

namespace CleanMenu.App;

public partial class MainWindow : Window
{
    public MainWindow(IOsVersionProvider osVersionProvider)
    {
        InitializeComponent();

        var profile = OsProfile.Detect(osVersionProvider);
        OsText.Text = profile.Detected switch
        {
            WindowsVersion.Windows11 => "Sistema detectado: Windows 11",
            WindowsVersion.Windows10 => "Sistema detectado: Windows 10",
            _ => "Sistema não suportado. É necessário Windows 10 (1809+) ou Windows 11.",
        };
    }
}
