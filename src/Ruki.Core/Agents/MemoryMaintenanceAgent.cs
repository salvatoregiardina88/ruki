using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Ruki.Core.Abstractions;
using Ruki.Core.Llm;
using Ruki.Core.Memory;

namespace Ruki.Core.Agents;

/// <summary>
/// Implementazione dell'agente di manutenzione. In un solo ciclo:
/// <list type="number">
///   <item>deduplica/unisce le memorie sovrapposte;</item>
///   <item>riorganizza l'albero delle categorie (riassegnando ogni memoria) per facilitare il recupero;</item>
///   <item>rimuove le categorie rimaste vuote.</item>
/// </list>
/// </summary>
public sealed class MemoryMaintenanceAgent : IMemoryMaintenanceAgent
{
    private const int PerMemoryContentLimit = 600;
    private const int TotalPromptLimit = 20_000;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private const string SystemPrompt =
        """
        You receive the list of Ruki's memories (with id, current path, title, summary and content).
        Do TWO things:

        1) DEDUPLICATE: find groups of DUPLICATE, strongly overlapping, or CONTAINED memories (one whose
           content is already covered by a more complete one — e.g. a single step that already exists
           inside a full procedure) and, for each, propose ONE unified memory (the ids to merge + merged
           title/summary/content). When merging, PRESERVE every concrete detail (URLs, account names,
           identifiers, exact values, labels) — never drop them. Do not merge memories about different
           topics/tools.

        2) REORGANIZE: design a clear, logical CATEGORY structure (from general to specific) meant to
           EASILY RETRIEVE information when needed, and assign EACH memory to the most suitable category
           path. Group related memories, reuse sensible category names, keep the tree tidy and not too
           deep. For merged memories use the kept id (the FIRST id of the group).

        Keep titles, summaries and content in their ORIGINAL language (do not translate them).

        Reply ONLY with JSON:
        {
          "merges": [ { "ids": ["id1","id2"], "title": "...", "summary": "...", "content": "..." } ],
          "placements": [ { "id": "id1", "categoryPath": ["Category", "Subcategory"] } ]
        }
        In "placements" include ALL memories (for merged ones use the kept id). If there is nothing to
        do, return empty lists.
        Titles (including merged ones) must be SHORT and specific and must NOT repeat the path/categories
        (no "/" in the title): those already live in categoryPath. No text outside the JSON.
        """;

    private readonly IMemoryStore _store;
    private readonly ILlmProvider _llm;
    private readonly ISettingsService _settings;
    private readonly ILogger<MemoryMaintenanceAgent> _logger;

    public MemoryMaintenanceAgent(
        IMemoryStore store, ILlmProvider llm, ISettingsService settings, ILogger<MemoryMaintenanceAgent> logger)
    {
        _store = store;
        _llm = llm;
        _settings = settings;
        _logger = logger;
    }

    public async Task<MaintenanceReport> RunAsync(CancellationToken cancellationToken = default)
    {
        var notes = new List<string>();
        var mergedGroups = 0;
        var removedMemories = 0;
        var movedMemories = 0;
        var removedCategories = 0;

        // Deduplica + riorganizzazione (servono almeno 2 memorie attive per chiamare il modello).
        var leaves = CollectLeaves();
        if (leaves.Count >= 2)
        {
            var request = new LlmRequest
            {
                SystemInstruction = SystemPrompt,
                Messages = [new ChatMessage(ChatRole.User, "Memories:\n\n" + Render(leaves))],
                Temperature = 0.2,
            };

            var response = await _llm.CompleteAsync(request, cancellationToken);
            var plan = ParsePlan(response.Text);

            (mergedGroups, removedMemories) = ApplyMerges(plan.Merges ?? [], notes);
            movedMemories = ApplyPlacements(plan.Placements ?? [], notes);
            removedCategories = RemoveEmptyCategories();
        }

        // Pruning "soft": archivia (non cancella) le memorie vecchie e poco usate.
        var obsoleted = PruneByDisuse(notes);

        // Registra quando la manutenzione è stata eseguita (mostrato nel tab Memoria).
        var updated = _settings.Current.Clone();
        updated.LastMemoryMaintenanceUtc = DateTimeOffset.UtcNow;
        _settings.Save(updated);

        var report = new MaintenanceReport(mergedGroups, removedMemories, movedMemories, removedCategories, obsoleted, notes);
        _logger.LogInformation("Manutenzione memoria: {Summary}", report.Summary);
        return report;
    }

    private int PruneByDisuse(List<string> notes)
    {
        var settings = _settings.Current;
        var threshold = DateTimeOffset.UtcNow.AddDays(-settings.ObsoleteAfterDays);
        var obsoleted = 0;

        foreach (var (node, _) in CollectLeaves())
        {
            var lastUsed = node.LastUsedAt ?? node.CreatedAt;
            if (node.UseCount < settings.ObsoleteMaxUses && lastUsed < threshold)
            {
                _store.SetObsolete(node.Id, true);
                obsoleted++;
            }
        }

        if (obsoleted > 0)
            notes.Add($"Archiviate {obsoleted} memorie non usate da oltre {settings.ObsoleteAfterDays} giorni.");
        return obsoleted;
    }

    // ---------------------------------------------------------------- deduplica

    private (int Merged, int Removed) ApplyMerges(List<MergeDto> merges, List<string> notes)
    {
        var mergedGroups = 0;
        var removed = 0;

        foreach (var merge in merges)
        {
            var ids = (merge.Ids ?? [])
                .Where(id => !string.IsNullOrWhiteSpace(id) && _store.GetNode(id) is not null)
                .Distinct()
                .ToList();
            if (ids.Count < 2)
                continue;

            // Manteniamo il primo nodo (lo aggiorniamo) ed eliminiamo gli altri.
            var keep = _store.GetNode(ids[0])!;
            keep.Title = string.IsNullOrWhiteSpace(merge.Title) ? keep.Title : merge.Title.Trim();
            keep.Summary = string.IsNullOrWhiteSpace(merge.Summary) ? keep.Summary : merge.Summary.Trim();
            keep.Content = string.IsNullOrWhiteSpace(merge.Content) ? keep.Content : merge.Content.Trim();
            _store.Update(keep);

            foreach (var id in ids.Skip(1))
            {
                _store.Delete(id);
                removed++;
            }

            mergedGroups++;
            notes.Add($"Unite {ids.Count} memorie in «{keep.Title}».");
        }

        return (mergedGroups, removed);
    }

    // ------------------------------------------------------------ riorganizza

    private int ApplyPlacements(List<PlacementDto> placements, List<string> notes)
    {
        var moved = 0;

        foreach (var placement in placements)
        {
            if (string.IsNullOrWhiteSpace(placement.Id) || placement.CategoryPath is not { Count: > 0 })
                continue;

            var node = _store.GetNode(placement.Id);
            if (node is null)
                continue;   // memoria unita/rimossa: la ignoriamo

            var targetParent = EnsureCategoryPath(placement.CategoryPath);

            // Evita titoli che ripetono il percorso: se il titolo inizia con "Cat / Sub / ", lo togliamo.
            var cleanTitle = StripCategoryPrefix(node.Title, placement.CategoryPath);
            if (cleanTitle != node.Title)
            {
                node.Title = cleanTitle;
                _store.Update(node);
            }

            if (node.ParentId != targetParent)
            {
                _store.Move(node.Id, targetParent);
                moved++;
            }
        }

        if (moved > 0)
            notes.Add($"Risistemate {moved} memorie nelle categorie.");
        return moved;
    }

    /// <summary>Toglie dal titolo un eventuale prefisso pari al percorso di categoria ("Cat / Sub / ").</summary>
    private static string StripCategoryPrefix(string title, IReadOnlyList<string> path)
    {
        var prefix = string.Join(" / ", path.Select(p => p.Trim())) + " / ";
        if (!title.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return title;

        var stripped = title[prefix.Length..].Trim();
        return stripped.Length == 0 ? title : stripped;
    }

    /// <summary>Crea (riusando per titolo) le categorie del percorso e restituisce l'id dell'ultima.</summary>
    private string? EnsureCategoryPath(IReadOnlyList<string> path)
    {
        string? parentId = null;
        foreach (var rawName in path)
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
        }

        return parentId;
    }

    /// <summary>Rimuove iterativamente le categorie senza figli (anche quelle svuotate dai sposta­menti).</summary>
    private int RemoveEmptyCategories()
    {
        var removed = 0;
        bool changed;
        do
        {
            changed = false;
            foreach (var category in CollectCategories())
            {
                if (_store.GetNode(category.Id) is null)
                    continue;   // già rimossa in questo giro
                if (_store.GetChildren(category.Id).Count == 0)
                {
                    _store.Delete(category.Id);
                    removed++;
                    changed = true;
                }
            }
        }
        while (changed);

        return removed;
    }

    // ---------------------------------------------------------------- helper

    private List<(MemoryNode Node, string Path)> CollectLeaves()
    {
        var leaves = new List<(MemoryNode, string)>();
        Walk(parentId: null, prefix: string.Empty);
        return leaves;

        void Walk(string? parentId, string prefix)
        {
            foreach (var child in _store.GetChildren(parentId))
            {
                var path = prefix.Length == 0 ? child.Title : $"{prefix} / {child.Title}";
                if (child.Type == MemoryNodeType.Category)
                {
                    Walk(child.Id, path);
                }
                else if (!child.IsObsolete
                    && !string.Equals(child.Title, UserProfileMemory.ProfileNodeTitle, StringComparison.OrdinalIgnoreCase)
                    && _store.GetNode(child.Id) is { } full)
                {
                    leaves.Add((full, path));   // ignoriamo le archiviate e il profilo utente (speciale)
                }
            }
        }
    }

    private List<MemoryNodeInfo> CollectCategories()
    {
        var categories = new List<MemoryNodeInfo>();
        Walk(parentId: null);
        return categories;

        void Walk(string? parentId)
        {
            foreach (var child in _store.GetChildren(parentId))
            {
                if (child.Type != MemoryNodeType.Category)
                    continue;
                categories.Add(child);
                Walk(child.Id);
            }
        }
    }

    private static string Render(IReadOnlyList<(MemoryNode Node, string Path)> leaves)
    {
        var builder = new StringBuilder();
        foreach (var (node, path) in leaves)
        {
            builder.AppendLine($"[{node.Id}] {path}");
            if (!string.IsNullOrWhiteSpace(node.Summary))
                builder.AppendLine($"Riassunto: {node.Summary}");

            var content = node.Content ?? string.Empty;
            builder.AppendLine($"Contenuto: {(content.Length > PerMemoryContentLimit ? content[..PerMemoryContentLimit] + "…" : content)}");
            builder.AppendLine("---");

            if (builder.Length > TotalPromptLimit)
                break;
        }

        return builder.ToString();
    }

    private PlanDto ParsePlan(string responseText)
    {
        var json = ExtractJsonObject(responseText);
        try
        {
            return JsonSerializer.Deserialize<PlanDto>(json, Json) ?? new PlanDto(null, null);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Risposta di manutenzione non interpretabile: nessuna modifica.");
            return new PlanDto(null, null);
        }
    }

    private static string ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : text;
    }

    private sealed record PlanDto(List<MergeDto>? Merges, List<PlacementDto>? Placements);

    private sealed record MergeDto(List<string>? Ids, string? Title, string? Summary, string? Content);

    private sealed record PlacementDto(string? Id, List<string>? CategoryPath);
}
