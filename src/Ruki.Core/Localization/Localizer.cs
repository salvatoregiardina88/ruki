namespace Ruki.Core.Localization;

/// <summary>
/// Localizzatore "leggero" per i messaggi prodotti fuori dalla UI (es. eccezioni del provider LLM),
/// che non possono usare il <c>Loc</c> della App. La lingua corrente è la **fonte di verità unica**:
/// la imposta la App (tramite il suo <c>LanguageManager</c>), e sia la UI sia questo livello la leggono.
/// </summary>
public static class Localizer
{
    /// <summary>Lingua corrente ("it" o "en").</summary>
    public static string Language { get; set; } = "it";

    /// <summary>Traduzione della chiave nella lingua corrente.</summary>
    public static string T(string key) => CoreStrings.Get(Language, key);

    /// <summary>Come <see cref="T(string)"/>, con formattazione dei valori (es. "Errore ({0}): {1}").</summary>
    public static string T(string key, params object?[] args) => string.Format(T(key), args);
}
