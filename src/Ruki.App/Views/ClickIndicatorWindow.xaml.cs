using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Ruki.App.Views;

/// <summary>
/// Finestrella overlay "click-through": l'anello rosso che segue il cursore durante l'esecuzione.
/// Gli stili estesi la rendono trasparente ai click e fanno sì che non rubi mai il focus.
/// </summary>
public partial class ClickIndicatorWindow : Window
{
    private static readonly Brush FilledBrush = CreateFilledBrush();

    public ClickIndicatorWindow() => InitializeComponent();

    /// <summary>Riempie o svuota il cerchio per segnalare un click.</summary>
    public void SetFilled(bool filled) => Ring.Fill = filled ? FilledBrush : Brushes.Transparent;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        exStyle |= WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new nint(exStyle));
    }

    private static Brush CreateFilledBrush()
    {
        var brush = new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0x2D, 0x2D));
        brush.Freeze();
        return brush;
    }

    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TRANSPARENT = 0x00000020;
    private const long WS_EX_LAYERED = 0x00080000;
    private const long WS_EX_NOACTIVATE = 0x08000000;
    private const long WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);
}
