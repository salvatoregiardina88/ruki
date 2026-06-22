namespace Ruki.Core.Automation;

/// <summary>
/// Fornisce, in modo best-effort, una descrizione di DOVE si trova il cursore di testo (caret)
/// nell'elemento attualmente a fuoco, tramite l'accessibilità del sistema (UI Automation).
/// <para>
/// Serve all'Action Agent: il caret lampeggiante spesso NON è catturato nello screenshot, perciò
/// l'agente può sbagliare riga quando modifica del testo. Questa "evidenza" gli dice qual è il campo
/// a fuoco, la riga corrente e l'eventuale selezione, così sa esattamente dove agiranno digitazione
/// e Canc/Backspace. Dove l'app non espone il testo via accessibilità, restituisce <c>null</c>.
/// </para>
/// </summary>
public interface ICaretContextProvider
{
    /// <summary>
    /// Breve descrizione in inglese (coerente col prompt dell'agente) del campo a fuoco e della
    /// posizione del caret, oppure <c>null</c> se non disponibile.
    /// </summary>
    string? Describe();
}
