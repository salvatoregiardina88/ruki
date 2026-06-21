namespace Ruki.Core.Agents;

/// <summary>Come si è concluso un compito dell'Action Agent.</summary>
public enum ActionOutcome
{
    /// <summary>Compito completato con successo.</summary>
    Completed,

    /// <summary>L'agente non è riuscito a completare il compito.</summary>
    Failed,

    /// <summary>Fermato perché ha raggiunto il limite massimo di passi.</summary>
    LimitReached,
}

/// <summary>
/// Esito dell'esecuzione di un compito da parte dell'Action Agent.
/// <para>
/// <see cref="Detail"/> è il messaggio prodotto dal modello (può mancare). I testi mostrati
/// all'utente vengono però composti e localizzati nella UI a partire da <see cref="Outcome"/>:
/// così l'Action Agent (nel layer Core) non genera stringhe nella lingua dell'interfaccia.
/// </para>
/// </summary>
public sealed record ActionResult(ActionOutcome Outcome, string? Detail, int Steps)
{
    /// <summary>Comodità: vero se il compito è stato completato.</summary>
    public bool Success => Outcome == ActionOutcome.Completed;
}
