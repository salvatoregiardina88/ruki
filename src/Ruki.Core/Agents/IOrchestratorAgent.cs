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
    /// Estrae dalla conversazione un profilo sintetico dell'utente e lo salva in memoria,
    /// così Ruki lo "ricorda" anche nelle sessioni future. Operazione best-effort.
    /// </summary>
    Task UpdateUserProfileAsync(CancellationToken cancellationToken = default);

    /// <summary>Azzera la conversazione corrente.</summary>
    void Reset();
}
