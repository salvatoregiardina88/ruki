namespace Ruki.Core.Agents;

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
    /// <summary>Restituisce true se l'utente approva l'azione descritta, false se la rifiuta.</summary>
    Task<bool> ConfirmAsync(string actionDescription, CancellationToken cancellationToken = default);
}
