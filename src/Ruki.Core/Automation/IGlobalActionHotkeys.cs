namespace Ruki.Core.Automation;

/// <summary>
/// Scorciatoie globali da tastiera per controllare l'agente dell'azione MENTRE è in esecuzione:
/// <c>Esc</c> = ferma, <c>Barra spaziatrice</c> = pausa/riprendi. Sono indispensabili perché durante
/// l'esecuzione è l'agente a controllare mouse e tastiera. Reagiscono solo ai tasti FISICI dell'utente:
/// gli stessi tasti iniettati dall'agente (es. spazi mentre digita) vengono ignorati.
/// </summary>
public interface IGlobalActionHotkeys
{
    /// <summary>
    /// Inizia ad ascoltare: invoca <paramref name="onStop"/> all'Esc fisico e
    /// <paramref name="onTogglePause"/> alla Barra spaziatrice fisica. Va chiamato dal thread UI
    /// (richiede un message loop).
    /// </summary>
    void Start(Action onStop, Action onTogglePause);

    /// <summary>Smette di ascoltare.</summary>
    void Stop();
}
