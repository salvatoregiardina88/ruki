using Microsoft.Extensions.Logging;
using Ruki.Core.Agents;
using Ruki.Core.Memory;
using Ruki.Core.Training;

namespace Ruki.Infrastructure.Sessions;

/// <summary>
/// Implementazione di <see cref="ITrainingPipeline"/>: collega la sessione registrata agli agenti.
/// Prepara la timeline e l'elenco delle categorie esistenti, fa analizzare il video al Training Agent
/// e fa salvare il risultato dal Memory Agent.
/// </summary>
public sealed class TrainingPipeline : ITrainingPipeline
{
    private readonly ITrainingAgent _trainingAgent;
    private readonly IMemoryAgent _memoryAgent;
    private readonly IMemoryStore _memory;
    private readonly ILogger<TrainingPipeline> _logger;

    public TrainingPipeline(
        ITrainingAgent trainingAgent,
        IMemoryAgent memoryAgent,
        IMemoryStore memory,
        ILogger<TrainingPipeline> logger)
    {
        _trainingAgent = trainingAgent;
        _memoryAgent = memoryAgent;
        _memory = memory;
        _logger = logger;
    }

    public async Task<int> ProcessSessionAsync(TrainingSessionInfo session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (string.IsNullOrEmpty(session.VideoPath) || !File.Exists(session.VideoPath))
            throw new InvalidOperationException("La sessione non ha un video da analizzare.");

        var timeline = SessionTimelineFormatter.Build(session.FolderPath);
        var existingCategories = CollectCategoryPaths();

        _logger.LogInformation("Avvio analisi della sessione {Id}.", session.Id);
        var knowledgeItems = await _trainingAgent.AnalyzeAsync(session.VideoPath, timeline, existingCategories, cancellationToken);

        // Una sessione può produrre più memorie: le salviamo tutte. Il dettaglio (titolo → percorso)
        // di ciascuna lo registra il Memory Agent nei log; all'utente basta il conteggio.
        foreach (var knowledge in knowledgeItems)
            _memoryAgent.Store(knowledge);

        _logger.LogInformation("Sessione {Id} appresa: {Count} memoria/e.", session.Id, knowledgeItems.Count);
        return knowledgeItems.Count;
    }

    /// <summary>Elenco "piatto" dei percorsi di categoria esistenti (es. "Software / Excel").</summary>
    private IReadOnlyList<string> CollectCategoryPaths()
    {
        var paths = new List<string>();
        Walk(parentId: null, prefix: string.Empty);
        return paths;

        void Walk(string? parentId, string prefix)
        {
            foreach (var child in _memory.GetChildren(parentId))
            {
                if (child.Type != MemoryNodeType.Category)
                    continue;

                var current = prefix.Length == 0 ? child.Title : $"{prefix} / {child.Title}";
                paths.Add(current);
                Walk(child.Id, current);
            }
        }
    }
}
