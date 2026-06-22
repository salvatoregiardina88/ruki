using Ruki.Core.Llm;

namespace Ruki.Core.Agents;

/// <summary>
/// Agente orchestratore: è l'agente con cui l'utente chatta. È conversazionale (mantiene
/// la cronologia della sessione) e farà da regia verso gli altri agenti.
/// <para>
/// La cronologia vive finché l'app è aperta e si azzera alla chiusura (è registrato come
/// singleton: la conversazione persiste anche riaprendo la finestra di chat, ma non oltre).
/// </para>
/// </summary>
public interface IOrchestratorAgent
{
    /// <summary>Messaggio di benvenuto mostrato all'apertura della chat (non fa parte della cronologia inviata al modello).</summary>
    string WelcomeMessage { get; }

    /// <summary>Cronologia dei turni utente/assistente, dal più vecchio al più recente.</summary>
    IReadOnlyList<ChatMessage> History { get; }

    /// <summary>
    /// Invia un messaggio dell'utente e restituisce la risposta dell'agente, con l'eventuale
    /// obiettivo da eseguire se l'utente ha chiesto di compiere un'azione sul PC.
    /// </summary>
    Task<OrchestratorReply> SendAsync(string userMessage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra in cronologia l'esito di un'azione eseguita sul PC, come se l'assistente avesse
    /// riferito il risultato. Serve a mantenere la conversazione coerente: i messaggi successivi
    /// dell'utente possono fare riferimento a ciò che l'azione ha prodotto. Va chiamato anche se la
    /// finestra di chat è chiusa (la cronologia vive nell'orchestratore, non nella UI).
    /// </summary>
    void NoteActionOutcome(string outcome);

    /// <summary>Azzera la conversazione corrente.</summary>
    void Reset();
}
