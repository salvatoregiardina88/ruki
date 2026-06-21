namespace Ruki.Core.Agents;

/// <summary>
/// Risposta dell'orchestratore: il messaggio per l'utente e, se l'utente ha chiesto di ESEGUIRE
/// un compito sul PC, l'obiettivo da passare all'Action Agent (altrimenti <c>null</c>).
/// </summary>
public sealed record OrchestratorReply(string Text, string? ActionGoal);
