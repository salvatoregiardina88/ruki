using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Ruki.App.Views;

namespace Ruki.App.Services;

/// <summary>
/// Implementazione di <see cref="IWindowService"/>.
/// <para>
/// Risolve le finestre dal container DI (così ricevono i loro ViewModel) e tiene
/// traccia di quelle aperte: se una finestra dello stesso tipo è già visibile, la
/// porta in primo piano invece di aprirne una seconda.
/// </para>
/// </summary>
public sealed class WindowService : IWindowService
{
    private readonly IServiceProvider _services;

    // Una sola istanza aperta per tipo di finestra. Si svuota quando la finestra si chiude.
    private readonly Dictionary<Type, Window> _openWindows = new();

    public WindowService(IServiceProvider services) => _services = services;

    // La chat è un overlay: la posizioniamo appena sopra la barra dell'overlay.
    public void ShowChat() => ShowSingleInstance<ChatWindow>(PositionAboveOverlay);

    public void ShowSettings() => ShowSingleInstance<SettingsWindow>();

    public void ShowActionDebug() => ShowSingleInstance<ActionDebugWindow>();

    /// <summary>
    /// Mostra una finestra garantendo che ne esista al massimo una istanza per tipo.
    /// </summary>
    /// <param name="position">Posizionamento opzionale applicato prima di mostrare la finestra.</param>
    private void ShowSingleInstance<TWindow>(Action<Window>? position = null) where TWindow : Window
    {
        var type = typeof(TWindow);

        // Già aperta: riportala in primo piano (e ripristinala se minimizzata).
        if (_openWindows.TryGetValue(type, out var existing))
        {
            if (existing.WindowState == WindowState.Minimized)
                existing.WindowState = WindowState.Normal;
            existing.Activate();
            return;
        }

        // Nuova istanza dal container (ottiene il proprio ViewModel via DI).
        var window = _services.GetRequiredService<TWindow>();
        window.Closed += (_, _) => _openWindows.Remove(type);
        _openWindows[type] = window;
        position?.Invoke(window);
        window.Show();
    }

    /// <summary>Posiziona una finestra appena sopra la barra dell'overlay, allineata a sinistra e dentro lo schermo.</summary>
    private void PositionAboveOverlay(Window window)
    {
        var overlay = _services.GetRequiredService<OverlayWindow>();
        var area = SystemParameters.WorkArea;

        // L'overlay è in basso: la finestra va sopra (il margine trasparente della scheda dà lo spazietto).
        var left = overlay.Left;
        var top = overlay.Top - window.Height;

        // Manteniamo la finestra dentro l'area di lavoro.
        left = Math.Max(area.Left, Math.Min(left, area.Right - window.Width));
        top = Math.Max(area.Top, top);

        window.Left = left;
        window.Top = top;
    }
}
