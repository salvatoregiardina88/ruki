namespace Ruki.Core.Agents;

/// <summary>
/// Risposta dell'orchestratore: il messaggio per l'utente, l'eventuale obiettivo da passare
/// all'Action Agent (se l'utente ha chiesto di ESEGUIRE un compito) e l'eventuale fatto DUREVOLE
/// da ricordare sull'utente (<see cref="ProfileNote"/>): se valorizzato, viene scritto in memoria.
/// </summary>
public sealed record OrchestratorReply(string Text, string? ActionGoal, string? ProfileNote = null);
