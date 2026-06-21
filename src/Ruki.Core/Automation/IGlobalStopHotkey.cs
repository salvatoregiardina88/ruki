namespace Ruki.Core.Automation;

/// <summary>
/// Scorciatoia globale per fermare l'agente dell'azione da tastiera (Esc), indispensabile perché
/// durante l'esecuzione è l'agente a controllare il mouse.
/// </summary>
public interface IGlobalStopHotkey
{
    /// <summary>Inizia ad ascoltare il tasto di stop; invoca <paramref name="onStop"/> quando premuto.
    /// Va chiamato dal thread UI (richiede un message loop).</summary>
    void Start(Action onStop);

    /// <summary>Smette di ascoltare.</summary>
    void Stop();
}
