namespace Ruki.Core.Llm;

/// <summary>
/// Astrazione del modello AI usata da tutti gli agenti. L'implementazione concreta
/// (oggi Gemini) vive nell'infrastruttura: gli agenti non sanno quale provider c'è dietro.
/// </summary>
public interface ILlmProvider
{
    /// <summary>
    /// Invia una richiesta al modello e restituisce la risposta testuale.
    /// Lancia <see cref="LlmException"/> in caso di errore recuperabile/comunicabile all'utente.
    /// </summary>
    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Carica un file presso il provider (es. un video) e ne restituisce il riferimento, già
    /// pronto per essere usato in una richiesta. Attende che il file sia elaborato e disponibile.
    /// </summary>
    Task<LlmFile> UploadFileAsync(string filePath, string mimeType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica che la chiave API configurata sia valida con una chiamata leggera (non consuma quota).
    /// Lancia <see cref="LlmException"/> se la chiave manca, non è valida o il servizio non è raggiungibile.
    /// </summary>
    Task ValidateKeyAsync(CancellationToken cancellationToken = default);
}
