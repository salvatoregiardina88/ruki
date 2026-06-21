namespace Ruki.Core.Agents;

/// <summary>
/// Agente dell'azione: esegue un compito sul PC con un loop "guarda lo schermo → decidi → agisci",
/// finché non lo completa, fallisce, o l'utente lo ferma tramite il <see cref="IActionController"/>.
/// </summary>
public interface IActionAgent
{
    /// <summary>
    /// Esegue il compito. L'agente naviga la memoria da sé (parte dall'albero dei titoli ed espande
    /// o legge i contenuti su richiesta) e mantiene una conversazione per tutta l'esecuzione.
    /// </summary>
    Task<ActionResult> RunAsync(string goal, IActionController controller);
}
