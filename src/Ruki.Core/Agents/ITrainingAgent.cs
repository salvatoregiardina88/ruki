namespace Ruki.Core.Agents;

/// <summary>
/// Agente di addestramento: analizza una sessione registrata (video con audio + timeline degli
/// eventi e della chat) ed estrae la conoscenza riutilizzabile.
/// </summary>
public interface ITrainingAgent
{
    /// <summary>
    /// Carica il video al modello, gli passa la timeline degli eventi e le categorie già esistenti
    /// (per suggerire una collocazione coerente) e restituisce le memorie estratte.
    /// <para>Una sessione può contenere più attività distinte: vengono restituite tutte.</para>
    /// </summary>
    Task<IReadOnlyList<LearnedKnowledge>> AnalyzeAsync(
        string videoPath,
        string eventTimeline,
        IReadOnlyList<string> existingCategories,
        CancellationToken cancellationToken = default);
}
