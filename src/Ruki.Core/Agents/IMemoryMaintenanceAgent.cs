namespace Ruki.Core.Agents;

/// <summary>
/// Agente di manutenzione della memoria: individua e unisce le memorie duplicate/sovrapposte,
/// mantenendo l'albero pulito. (Il pruning delle memorie poco usate è volutamente escluso per ora,
/// per non cancellare dati dell'utente senza conferma.)
/// </summary>
public interface IMemoryMaintenanceAgent
{
    Task<MaintenanceReport> RunAsync(CancellationToken cancellationToken = default);
}
