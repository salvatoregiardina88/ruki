namespace Ruki.Core.Abstractions;

/// <summary>Token consumati nel mese corrente, separati tra input (inviati) e output (ricevuti).</summary>
public sealed record UsageSnapshot(long InputTokens, long OutputTokens);

/// <summary>
/// Tiene il conteggio dei token consumati nel MESE corrente (input/output separati), per stimare
/// il costo. Si azzera automaticamente a inizio mese. Lo stato è persistito su disco.
/// </summary>
public interface IUsageTracker
{
    /// <summary>Uso del mese corrente (azzerato in automatico al cambio di mese).</summary>
    UsageSnapshot Current { get; }

    /// <summary>Aggiunge i token di una chiamata al conteggio del mese corrente.</summary>
    void Record(long inputTokens, long outputTokens);

    /// <summary>Sollevato quando il conteggio cambia, per aggiornare la UI.</summary>
    event EventHandler<UsageSnapshot>? Changed;
}
