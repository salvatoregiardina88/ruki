namespace Ruki.Core.Llm;

/// <summary>
/// Richiesta verso il modello AI. È volutamente minimale (testo) ma pensata per crescere:
/// in futuro qui si aggiungeranno immagini/video e definizioni di funzioni (function calling).
/// </summary>
public sealed class LlmRequest
{
    /// <summary>
    /// Istruzione di sistema: definisce ruolo e comportamento del modello. Resta separata
    /// dalla cronologia perché non è un "turno" della conversazione.
    /// </summary>
    public string? SystemInstruction { get; init; }

    /// <summary>Cronologia della conversazione, dal più vecchio al più recente.</summary>
    public IReadOnlyList<ChatMessage> Messages { get; init; } = [];

    /// <summary>
    /// File (es. un video) da allegare alla richiesta. Vengono associati all'ultimo turno utente.
    /// </summary>
    public IReadOnlyList<LlmFile>? Files { get; init; }

    /// <summary>
    /// Immagini inline (es. screenshot) da allegare alla richiesta, associate all'ultimo turno utente.
    /// </summary>
    public IReadOnlyList<LlmImage>? Images { get; init; }

    /// <summary>Temperatura di campionamento (0 = deterministico). <c>null</c> = default del modello.</summary>
    public double? Temperature { get; init; }
}
