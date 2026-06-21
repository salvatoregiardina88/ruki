using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ruki.Core.Memory;

namespace Ruki.Core.Agents;

/// <summary>
/// Implementazione dell'agente della memoria. Percorre il <see cref="LearnedKnowledge.CategoryPath"/>
/// creando le categorie mancanti (riusando quelle esistenti per titolo) e aggiunge la memoria foglia.
/// </summary>
public sealed class MemoryAgent : IMemoryAgent
{
    private readonly IMemoryStore _store;
    private readonly ILogger<MemoryAgent> _logger;

    public MemoryAgent(IMemoryStore store, ILogger<MemoryAgent> logger)
    {
        _store = store;
        _logger = logger;
    }

    public string Store(LearnedKnowledge knowledge)
    {
        ArgumentNullException.ThrowIfNull(knowledge);

        // Scendi/crea il percorso di categorie.
        string? parentId = null;
        var pathTitles = new List<string>();
        foreach (var rawName in knowledge.CategoryPath)
        {
            var name = rawName.Trim();
            if (name.Length == 0)
                continue;

            var existing = _store.GetChildren(parentId).FirstOrDefault(child =>
                child.Type == MemoryNodeType.Category &&
                string.Equals(child.Title, name, StringComparison.OrdinalIgnoreCase));

            parentId = existing?.Id ?? _store.Add(new MemoryNode
            {
                Title = name,
                Type = MemoryNodeType.Category,
                ParentId = parentId,
            }).Id;

            pathTitles.Add(name);
        }

        // Aggiungi la memoria foglia con il contenuto esteso e il tipo (procedura/nozione) nei metadati.
        _store.Add(new MemoryNode
        {
            Title = knowledge.Title,
            Type = MemoryNodeType.Memory,
            ParentId = parentId,
            Summary = string.IsNullOrWhiteSpace(knowledge.Summary) ? null : knowledge.Summary,
            Content = knowledge.Content,
            Metadata = string.IsNullOrWhiteSpace(knowledge.Kind) ? null : JsonSerializer.Serialize(new { kind = knowledge.Kind }),
        });
        pathTitles.Add(knowledge.Title);

        var path = string.Join(" / ", pathTitles);
        _logger.LogInformation("Conoscenza salvata in memoria: {Path}.", path);
        return path;
    }
}
