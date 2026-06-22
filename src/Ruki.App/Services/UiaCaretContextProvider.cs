using System.Text;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using Microsoft.Extensions.Logging;
using Ruki.Core.Automation;

namespace Ruki.App.Services;

/// <summary>
/// Implementazione di <see cref="ICaretContextProvider"/> tramite UI Automation: legge dall'elemento
/// a fuoco il <see cref="TextPattern"/> (se esposto) per descrivere il campo, la riga su cui si trova
/// il caret e l'eventuale selezione. È tutto best-effort: se l'app non espone il testo via
/// accessibilità (succede con alcune app/editor web), restituisce <c>null</c> e l'agente prosegue
/// con il solo screenshot. Va invocata da un thread in background (l'Action Agent gira fuori dalla UI).
/// </summary>
public sealed class UiaCaretContextProvider : ICaretContextProvider
{
    private const int MaxLineChars = 200;
    private const int MaxSelectionChars = 200;

    private readonly ILogger<UiaCaretContextProvider> _logger;

    public UiaCaretContextProvider(ILogger<UiaCaretContextProvider> logger) => _logger = logger;

    public string? Describe()
    {
        try
        {
            var element = AutomationElement.FocusedElement;
            if (element is null)
                return null;

            // Solo gli elementi che espongono il testo "ricco" (TextPattern) ci danno il caret: per i
            // controlli non testuali lo screenshot e la finestra attiva sono già sufficienti.
            if (!element.TryGetCurrentPattern(TextPattern.Pattern, out var pattern) || pattern is not TextPattern textPattern)
                return null;

            var field = FieldName(element);

            TextPatternRange[] selection;
            try { selection = textPattern.GetSelection(); }
            catch { return $"Text focus: editing in {field}."; }

            if (selection.Length == 0)
                return $"Text focus: editing in {field}.";

            var caret = selection[0];
            var selected = Clean(SafeGetText(caret, MaxSelectionChars));

            // Espandiamo una COPIA del range alla riga che lo contiene: così l'agente sa esattamente
            // su quale riga agirà (evita di premere Canc/Backspace sulla riga sbagliata).
            var line = caret.Clone();
            try { line.ExpandToEnclosingUnit(TextUnit.Line); }
            catch { /* l'unità "riga" non è supportata da tutti i provider */ }
            var lineText = Clean(SafeGetText(line, MaxLineChars));

            var builder = new StringBuilder();
            builder.Append("Text focus: editing in ").Append(field).Append('.');
            if (lineText.Length > 0)
                builder.Append(" Caret is on this line: \"").Append(lineText).Append("\".");
            builder.Append(selected.Length > 0 ? $" Selected text: \"{selected}\"." : " No text is selected.");
            return builder.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Lettura del contesto del caret (UIA) non riuscita.");
            return null;
        }
    }

    /// <summary>Nome leggibile del campo a fuoco: etichetta accessibile, altrimenti il tipo di controllo.</summary>
    private static string FieldName(AutomationElement element)
    {
        try
        {
            var name = element.Current.Name;
            if (!string.IsNullOrWhiteSpace(name))
                return $"\"{name.Trim()}\"";
        }
        catch { /* elemento stale: passiamo al fallback */ }

        try
        {
            var type = element.Current.LocalizedControlType;
            if (!string.IsNullOrWhiteSpace(type))
                return $"a {type}";
        }
        catch { /* idem */ }

        return "a text field";
    }

    private static string SafeGetText(TextPatternRange range, int maxChars)
    {
        try { return range.GetText(maxChars) ?? string.Empty; }
        catch { return string.Empty; }
    }

    /// <summary>Riduce il testo a una riga (per il prompt): niente a capo, spazi ai bordi rimossi.</summary>
    private static string Clean(string text)
        => text.Replace("\r", " ").Replace("\n", " ").Trim();
}
