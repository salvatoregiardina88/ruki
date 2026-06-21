namespace Ruki.Core.Agents;

/// <summary>
/// Agente della memoria: colloca la conoscenza appresa nell'albero, creando le categorie mancanti.
/// (La riorganizzazione/deduplica periodica è una responsabilità separata, futura.)
/// </summary>
public interface IMemoryAgent
{
    /// <summary>Salva la conoscenza e restituisce il percorso testuale dove è stata collocata.</summary>
    string Store(LearnedKnowledge knowledge);
}
