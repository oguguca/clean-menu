using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CleanMenu.UI.Theming;

/// <summary>
/// Makes the native window title bar follow the app theme. Keeps the standard Windows frame
/// (snap layouts, accessibility) instead of drawing a custom one.
/// </summary>
public static partial class WindowTheme
{
    // DWMWA_USE_IMMERSIVE_DARK_MODE: 20 on Windows 10 20H1+ and Windows 11; 19 on Windows 10 1809-1909.
    private const int DarkModeAttribute = 20;
    private const int DarkModeAttributeLegacy = 19;

    // Windows 11 only: explicit caption colors. They also override the "accent color on title bars"
    // system option, which otherwise paints the bar with the user's accent color.
    private const int BorderColorAttribute = 34;
    private const int CaptionColorAttribute = 35;
    private const int TextColorAttribute = 36;

    // COLORREF is 0x00BBGGRR. Values match Brush.Sidebar / Brush.Text of each theme.
    private const int DarkCaption = 0x001E1815;   // #15181E
    private const int DarkText = 0x00F4F0EE;      // #EEF0F4
    private const int LightCaption = 0x00FFFFFF;  // #FFFFFF
    private const int LightText = 0x001E1815;     // #15181E
    private const int DarkBorder = 0x003C302A;    // #2A303C
    private const int LightBorder = 0x00E8E2DF;   // #DFE2E8

    public static void ApplyTitleBar(Window window, bool dark)
    {
        ArgumentNullException.ThrowIfNull(window);

        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return; // Not shown yet; call again from SourceInitialized.
        }

        // Every call below is cosmetic: failure (unsupported build) is ignored on purpose.
        var value = dark ? 1 : 0;
        if (DwmSetWindowAttribute(handle, DarkModeAttribute, ref value, sizeof(int)) != 0)
        {
            _ = DwmSetWindowAttribute(handle, DarkModeAttributeLegacy, ref value, sizeof(int));
        }

        var caption = dark ? DarkCaption : LightCaption;
        var text = dark ? DarkText : LightText;
        var border = dark ? DarkBorder : LightBorder;
        _ = DwmSetWindowAttribute(handle, BorderColorAttribute, ref border, sizeof(int));
        _ = DwmSetWindowAttribute(handle, CaptionColorAttribute, ref caption, sizeof(int));
        _ = DwmSetWindowAttribute(handle, TextColorAttribute, ref text, sizeof(int));
    }

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
