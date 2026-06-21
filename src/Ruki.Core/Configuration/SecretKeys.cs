namespace Ruki.Core.Configuration;

/// <summary>
/// Chiavi logiche con cui i segreti vengono memorizzati nell'<c>ISecretStore</c>.
/// Centralizzarle qui evita stringhe "magiche" sparse nel codice e refusi.
/// </summary>
public static class SecretKeys
{
    /// <summary>Chiave API di Google Gemini.</summary>
    public const string GeminiApiKey = "gemini-api-key";
}
