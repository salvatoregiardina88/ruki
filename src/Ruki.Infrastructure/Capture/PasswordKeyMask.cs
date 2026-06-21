namespace Ruki.Infrastructure.Capture;

/// <summary>
/// Maschera i tasti digitati in un campo password, così la password reale non finisce mai nel log
/// degli eventi né viene inviata al modello. I tasti di navigazione/invio (che non rivelano la
/// password) vengono mantenuti, utili a ricostruire la procedura.
/// </summary>
public static class PasswordKeyMask
{
    /// <summary>Carattere usato al posto di ogni tasto della password.</summary>
    public const string MaskChar = "•";

    /// <summary>
    /// Restituisce il nome del tasto da registrare: invariato se non si è in un campo password,
    /// altrimenti mascherato (tranne i tasti di navigazione Enter/Tab/Esc).
    /// </summary>
    public static string Apply(string keyName, bool inPasswordField)
        => inPasswordField && keyName is not ("Enter" or "Tab" or "Esc")
            ? MaskChar
            : keyName;
}
