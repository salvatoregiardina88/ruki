namespace Ruki.Core.Llm;

/// <summary>
/// Errore proveniente dal provider del modello AI (chiave mancante, errore HTTP,
/// risposta bloccata o vuota…). Pensata per avere messaggi mostrabili all'utente.
/// </summary>
public sealed class LlmException : Exception
{
    public LlmException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
