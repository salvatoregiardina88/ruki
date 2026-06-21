namespace Ruki.Core.Capture;

/// <summary>
/// Sorgente di eventi globali del PC (mouse, tastiera, cambio finestra in primo piano).
/// L'implementazione installa hook di sistema; gli eventi arrivano tramite <see cref="EventCaptured"/>.
/// </summary>
public interface IInputEventSource
{
    /// <summary>Sollevato a ogni evento catturato.</summary>
    event Action<InputEvent>? EventCaptured;

    /// <summary>Installa gli hook. Va chiamato da un thread con un message loop (es. il thread UI).</summary>
    void Start();

    /// <summary>Rimuove gli hook.</summary>
    void Stop();
}
