namespace Ruki.Core.Memory;

/// <summary>
/// Gestisce il "Profilo utente" in memoria: un piccolo insieme di fatti DUREVOLI sull'utente (chi è,
/// che lavoro fa, gli strumenti che usa abitualmente, preferenze stabili). È condiviso da più agenti
/// (orchestratore in chat, agente di addestramento): ognuno decide SE e QUANDO ricordare qualcosa, e
/// la scrittura è sempre un MERGE parsimonioso (unisce/aggiorna senza sovrascrivere né accumulare
/// dettagli di contesto effimero).
/// </summary>
public interface IUserProfileMemory
{
    /// <summary>Contenuto del profilo se ATTIVO (non archiviato dall'utente), altrimenti <c>null</c>.</summary>
    string? GetActiveProfile();

    /// <summary>
    /// Unisce un fatto DUREVOLE nel profilo (preservando ciò che è ancora valido, deduplicando). È
    /// best-effort: se il fatto è effimero/non rilevante il profilo resta invariato. Non lancia.
    /// </summary>
    Task RememberAsync(string durableFact, CancellationToken cancellationToken = default);
}
