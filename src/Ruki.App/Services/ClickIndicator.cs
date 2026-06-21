using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Ruki.App.Views;
using Ruki.Core.Automation;

namespace Ruki.App.Services;

/// <summary>
/// Implementazione di <see cref="IClickIndicator"/>: gestisce una finestrella overlay che segue il
/// cursore (aggiornata ~60 volte al secondo) e si riempie durante i click.
/// </summary>
public sealed class ClickIndicator : IClickIndicator
{
    private ClickIndicatorWindow? _window;
    private DispatcherTimer? _followTimer;

    public void Start() => OnUi(() =>
    {
        _window ??= new ClickIndicatorWindow();
        _window.SetFilled(false);
        UpdatePosition();
        _window.Show();

        _followTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _followTimer.Tick -= OnTick;
        _followTimer.Tick += OnTick;
        _followTimer.Start();
    });

    public void Stop() => OnUi(() =>
    {
        _followTimer?.Stop();
        _window?.Hide();
    });

    public void SetClicking(bool clicking) => OnUi(() => _window?.SetFilled(clicking));

    private void OnTick(object? sender, EventArgs e) => UpdatePosition();

    /// <summary>Posiziona l'anello centrato sul cursore (convertendo i pixel fisici in unità WPF).</summary>
    private void UpdatePosition()
    {
        if (_window is null || !GetCursorPos(out var point))
            return;

        var scale = GetDpiForSystem() / 96.0;
        _window.Left = point.X / scale - _window.Width / 2;
        _window.Top = point.Y / scale - _window.Height / 2;
    }

    private static void OnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
            return;
        if (dispatcher.CheckAccess())
            action();
        else
            dispatcher.InvokeAsync(action);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();
}
