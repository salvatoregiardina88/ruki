using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Ruki.App.Localization;
using Ruki.App.Views;
using Ruki.Core.Agents;

namespace Ruki.App.Services;

/// <summary>
/// Implementazione UI di <see cref="IActionConfirmation"/>: mostra un dialogo modale Sì/No SEMPRE in
/// primo piano, descrivendo l'azione rischiosa nella lingua dell'utente. Per i click/scroll evidenzia
/// con un cerchio rosso lampeggiante il punto dove si vorrebbe agire (le coordinate grezze non dicono
/// nulla all'utente). L'Action Agent gira su un thread in background, quindi marshalliamo sul Dispatcher.
/// </summary>
public sealed class DialogActionConfirmation : IActionConfirmation
{
    public Task<bool> ConfirmAsync(RiskyAction action, CancellationToken cancellationToken = default)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
            return Task.FromResult(true);   // nessuna UI (es. in test): non blocchiamo l'esecuzione

        return dispatcher.InvokeAsync(() => Ask(action)).Task;
    }

    private static bool Ask(RiskyAction action)
    {
        // Cerchio rosso lampeggiante sul punto del click (se è un click/scroll), spento alla risposta.
        var highlight = TargetHighlight.TryCreate(action);
        try
        {
            var window = new ConfirmActionWindow(Loc.T("Confirm_Body", Describe(action)));
            return window.ShowDialog() == true;
        }
        finally
        {
            highlight?.Stop();
        }
    }

    /// <summary>Descrizione localizzata dell'azione; la posizione del click è data dal cerchio, non da numeri.</summary>
    private static string Describe(RiskyAction action) => action.Type switch
    {
        AgentActionType.Click or AgentActionType.DoubleClick or AgentActionType.RightClick => Loc.T("Confirm_ActClick"),
        AgentActionType.Scroll => Loc.T("Confirm_ActScroll"),
        AgentActionType.Type => Loc.T("Confirm_ActType", action.Text ?? string.Empty),
        AgentActionType.Key => Loc.T("Confirm_ActKey", action.Text ?? string.Empty),
        _ => Loc.T("Confirm_ActOther"),
    };

    /// <summary>Cerchio rosso click-through che lampeggia su un punto fisso dello schermo finché non è fermato.</summary>
    private sealed class TargetHighlight
    {
        private readonly ClickIndicatorWindow _window;
        private readonly DispatcherTimer _blink;

        private TargetHighlight(int screenX, int screenY)
        {
            // Pixel fisici → unità WPF (come l'anello che segue il cursore): coerente sul monitor primario.
            var scale = GetDpiForSystem() / 96.0;
            _window = new ClickIndicatorWindow();
            _window.SetFilled(true);
            _window.Left = screenX / scale - _window.Width / 2;
            _window.Top = screenY / scale - _window.Height / 2;
            _window.Show();

            _blink = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
            _blink.Tick += (_, _) => _window.Opacity = _window.Opacity > 0.5 ? 0.12 : 1.0;
            _blink.Start();
        }

        public static TargetHighlight? TryCreate(RiskyAction action)
            => action.ScreenX is { } x && action.ScreenY is { } y ? new TargetHighlight(x, y) : null;

        public void Stop()
        {
            _blink.Stop();
            _window.Close();
        }

        [DllImport("user32.dll")]
        private static extern uint GetDpiForSystem();
    }
}
