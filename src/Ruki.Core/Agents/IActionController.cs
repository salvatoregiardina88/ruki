namespace Ruki.Core.Agents;

/// <summary>
/// Comando di controllo dell'esecuzione dell'Action Agent: consente all'utente di mettere in pausa
/// o fermare in qualsiasi momento. Ne esiste un'istanza per ogni esecuzione.
/// </summary>
public interface IActionController
{
    /// <summary>Token che viene annullato quando l'utente preme Stop.</summary>
    CancellationToken Token { get; }

    bool IsPaused { get; }

    void Pause();
    void Resume();

    /// <summary>Ferma l'esecuzione (annulla il <see cref="Token"/>).</summary>
    void Stop();

    /// <summary>Attende finché è in pausa; ritorna subito se non lo è. Rispetta Stop.</summary>
    Task WaitWhilePausedAsync(CancellationToken cancellationToken);
}
