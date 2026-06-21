namespace Ruki.Core.Llm;

/// <summary>
/// Immagine inviata "inline" nella richiesta (es. uno screenshot per l'agente che pilota il PC),
/// con i byte direttamente nel corpo della richiesta anziché tramite la Files API.
/// </summary>
public sealed record LlmImage(byte[] Data, string MimeType);
