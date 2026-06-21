namespace Ruki.Core.Abstractions;

/// <summary>
/// Archivio sicuro per i segreti dell'applicazione (es. la API key di Gemini).
/// <para>
/// L'implementazione cifra i valori a riposo e li lega all'utente Windows corrente,
/// così non finiscono mai in chiaro su disco. Gli agenti e la UI dipendono da questa
/// astrazione e non sanno NULLA di come avvenga la cifratura.
/// </para>
/// <para>
/// Le <paramref name="key"/> sono nomi logici stabili (vedi <c>SecretKeys</c>),
/// non i segreti stessi.
/// </para>
/// </summary>
public interface ISecretStore
{
    /// <summary>Salva (o sovrascrive) il segreto associato alla chiave indicata.</summary>
    void Set(string key, string secret);

    /// <summary>
    /// Restituisce il segreto associato alla chiave, oppure <c>null</c> se non esiste
    /// o non è decifrabile (es. file copiato su un altro account/PC).
    /// </summary>
    string? Get(string key);

    /// <summary>Indica se esiste un segreto per la chiave indicata.</summary>
    bool Has(string key);

    /// <summary>Elimina il segreto associato alla chiave, se presente.</summary>
    void Delete(string key);
}
