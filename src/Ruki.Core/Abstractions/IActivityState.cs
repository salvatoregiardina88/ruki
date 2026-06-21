namespace Ruki.Core.Abstractions;

/// <summary>Stato corrente di Ruki, di cui l'orchestratore deve essere consapevole in chat.</summary>
public enum RukiActivity
{
    /// <summary>Chat normale: nessun addestramento e nessun compito in esecuzione.</summary>
    Idle,

    /// <summary>È in corso una registrazione di addestramento (l'utente sta insegnando).</summary>
    Training,

    /// <summary>L'agente dell'azione sta eseguendo un compito sul PC.</summary>
    Executing,
}

/// <summary>
/// Espone lo stato corrente di Ruki (chat / addestramento / esecuzione). Implementato nel layer App
/// (che conosce registrazione e sessione d'azione) e usato dall'orchestratore per regolarsi.
/// </summary>
public interface IActivityState
{
    RukiActivity Current { get; }
}
