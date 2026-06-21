using System.Globalization;

namespace Ruki.App.Localization;

/// <summary>
/// Applica in un colpo solo la lingua scelta dall'utente: aggiorna le stringhe della UI
/// (<see cref="Loc"/>) e la cultura dell'interfaccia (<see cref="CultureInfo.CurrentUICulture"/>),
/// da cui dipendono testi generati fuori dalla UI come il messaggio di benvenuto dell'orchestratore.
/// <para>
/// Tocchiamo solo la <c>CurrentUICulture</c> (lingua), non la <c>CurrentCulture</c> (formato di
/// numeri/date): così la lingua cambia senza alterare, ad esempio, il separatore decimale.
/// </para>
/// </summary>
public static class LanguageManager
{
    /// <summary>Normalizza un valore di lingua a "it" o "en" ("it" come default).</summary>
    public static string Normalize(string? language)
        => string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "it";

    /// <summary>Imposta la lingua dell'app (stringhe UI + cultura dell'interfaccia).</summary>
    public static void Apply(string? language)
    {
        var lang = Normalize(language);
        Loc.Instance.Language = lang;

        var culture = CultureInfo.GetCultureInfo(lang == "en" ? "en-US" : "it-IT");
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}
