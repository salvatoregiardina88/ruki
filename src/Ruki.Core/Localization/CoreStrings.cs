namespace Ruki.Core.Localization;

/// <summary>
/// Messaggi (IT/EN) prodotti dal dominio/infrastruttura e mostrati all'utente — soprattutto le
/// eccezioni del provider Gemini visibili in chat o nelle Impostazioni. Le stringhe della UI restano
/// invece nella tabella della App. Nei valori, <c>{0}</c>… sono segnaposto di <see cref="string.Format(string, object?[])"/>.
/// </summary>
internal static class CoreStrings
{
    private static readonly Dictionary<string, (string It, string En)> Table = new(StringComparer.Ordinal)
    {
        ["Llm_NoApiKey"] = ("Chiave API Gemini non configurata. Inseriscila in Impostazioni → API.",
                            "Gemini API key not configured. Add it in Settings → API."),
        ["Llm_NetworkError"] = ("Impossibile contattare Gemini (problema di rete o timeout). Controlla la connessione e riprova.",
                               "Couldn't reach Gemini (network problem or timeout). Check your connection and try again."),
        ["Llm_ApiError"] = ("Errore dal modello Gemini ({0}): {1}", "Error from the Gemini model ({0}): {1}"),
        ["Llm_InvalidKey"] = ("Chiave API non valida ({0}): {1}", "Invalid API key ({0}): {1}"),
        ["Llm_Blocked"] = ("Risposta bloccata dal modello (motivo: {0}).", "Response blocked by the model (reason: {0})."),
        ["Llm_NoText"] = ("Il modello non ha prodotto testo (finishReason: {0}).", "The model produced no text (finishReason: {0})."),
        ["Llm_EmptyResponse"] = ("Il modello ha restituito una risposta vuota.", "The model returned an empty response."),
        ["Llm_Unparseable"] = ("Risposta di Gemini non interpretabile.", "Gemini's response could not be parsed."),
    };

    public static string Get(string language, string key)
        => Table.TryGetValue(key, out var v)
            ? (string.Equals(language, "en", StringComparison.Ordinal) ? v.En : v.It)
            : key;
}
