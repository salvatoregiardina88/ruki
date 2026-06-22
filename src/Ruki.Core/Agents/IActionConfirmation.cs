namespace Ruki.Core.Agents;

/// <summary>
/// Azione rischiosa da confermare. Porta il tipo e il contenuto (per descriverla all'utente nella
/// sua lingua) e, per i click/scroll, le coordinate in PIXEL sullo schermo del bersaglio, così la UI
/// può evidenziare con un cerchio il punto dove si vorrebbe cliccare (le coordinate grezze non dicono
/// nulla all'utente).
/// </summary>
public sealed record RiskyAction(
    AgentActionType Type,
    string? Text = null,
    int? ScreenX = null,
    int? ScreenY = null);

/// <summary>
/// Chiede all'utente di confermare un'azione potenzialmente rischiosa prima di eseguirla.
/// <para>
/// È implementata nella UI (mostra un dialogo) e usata dall'Action Agent quando l'impostazione
/// <see cref="Configuration.RukiSettings.ConfirmRiskyActions"/> è attiva e il modello ha marcato
/// l'azione come rischiosa.
/// </para>
/// </summary>
public interface IActionConfirmation
{
    /// <summary>Restituisce true se l'utente approva l'azione, false se la rifiuta.</summary>
    Task<bool> ConfirmAsync(RiskyAction action, CancellationToken cancellationToken = default);
}
