namespace Ruki.Core.Automation;

/// <summary>
/// Scorciatoie globali da tastiera per controllare l'agente dell'azione MENTRE è in esecuzione:
/// <c>Esc</c> = ferma, <c>Barra spaziatrice</c> = metti in pausa (la RIPRESA è solo manuale, dal
/// pulsante nell'overlay). Valgono solo se il compito è in corso e NON in pausa: quando è in pausa
/// l'utente riprende il controllo della tastiera, quindi i tasti passano normalmente all'app.
/// Reagiscono solo ai tasti FISICI dell'utente: quelli iniettati dall'agente (es. spazi mentre
/// digita) vengono ignorati.
/// </summary>
public interface IGlobalActionHotkeys
{
    /// <summary>
    /// Inizia ad ascoltare: Esc fisico → <paramref name="onStop"/>, Barra spaziatrice fisica →
    /// <paramref name="onPause"/>. Entrambi agiscono SOLO se <paramref name="isPaused"/> restituisce
    /// false (mentre è in pausa i tasti passano all'app). Va chiamato dal thread UI (richiede un message loop).
    /// </summary>
    void Start(Action onStop, Action onPause, Func<bool> isPaused);

    /// <summary>Smette di ascoltare.</summary>
    void Stop();
}
