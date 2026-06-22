using System.Text;

namespace Ruki.Core.Llm;

/// <summary>
/// Piccoli aiuti per "riparare" il JSON prodotto dai modelli, che a volte non rispetta lo standard.
/// </summary>
public static class JsonText
{
    /// <summary>
    /// Esce i caratteri di controllo (a capo, tab, ritorno a capo…) lasciati GREZZI dentro le stringhe
    /// JSON. I modelli scrivono spesso campi come "thought" o "reply" su più righe senza escaparli, e
    /// questo rende il JSON non valido per un parser rigoroso (System.Text.Json). Qui camminiamo il
    /// testo carattere per carattere e, SOLO quando siamo dentro una stringa, trasformiamo i caratteri
    /// di controllo nella loro forma escapata; fuori dalle stringhe (dove gli a capo sono solo
    /// formattazione valida) non tocchiamo nulla. Le sequenze già escapate vengono preservate.
    /// </summary>
    public static string RepairControlChars(string json)
    {
        if (string.IsNullOrEmpty(json))
            return json;

        var builder = new StringBuilder(json.Length + 16);
        var inString = false;   // siamo dentro una stringa JSON?
        var escaped = false;    // il carattere precedente era un backslash di escape?

        foreach (var c in json)
        {
            if (!inString)
            {
                if (c == '"')
                    inString = true;
                builder.Append(c);
                continue;
            }

            // Dentro una stringa.
            if (escaped)
            {
                // Carattere che segue un backslash: fa parte di una sequenza di escape, lo lasciamo com'è.
                builder.Append(c);
                escaped = false;
                continue;
            }

            switch (c)
            {
                case '\\':
                    builder.Append(c);
                    escaped = true;
                    break;
                case '"':
                    builder.Append(c);
                    inString = false;
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (c < 0x20)
                        builder.Append("\\u").Append(((int)c).ToString("x4"));
                    else
                        builder.Append(c);
                    break;
            }
        }

        return builder.ToString();
    }
}
