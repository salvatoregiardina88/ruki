namespace Ruki.Core.Llm;

/// <summary>
/// Riferimento a un file caricato presso il provider (es. un video sulla Files API di Gemini),
/// usabile come parte di una richiesta multimodale.
/// </summary>
public sealed record LlmFile(string Uri, string MimeType);
