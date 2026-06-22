using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Ruki.Core.Llm;
using Ruki.Core.Memory;

namespace Ruki.Core.Agents;

/// <summary>
/// Implementazione dell'agente di addestramento. Costruisce la richiesta multimodale per il modello
/// (video + timeline) e ne interpreta la risposta in un oggetto <see cref="LearnedKnowledge"/>.
/// </summary>
public sealed class TrainingAgent : ITrainingAgent
{
    private const string VideoMimeType = "video/mp4";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private const string SystemPrompt =
        """
        You receive the recording of a session in which a user performs a task on the computer:
        a VIDEO with audio (the user's spoken comments) and a textual TIMELINE of the PC events
        (clicks, typed text, scrolling, window switches) and chat messages, synced with the video.

        GOAL: extract the useful knowledge so that in the future the agent can both REPRODUCE the tasks
        and REASON about new requests.

        SOURCE OF TRUTH: the VIDEO/screenshots are PRIMARY; the events timeline (keystrokes, clicks)
        only helps with order and timing. When they disagree, or when the screen shows more (the real
        URL in the address bar, exact labels, the resulting state), the SCREEN wins. Never reconstruct
        a step from the typed keys alone — read what actually happened on screen.

        Create SELF-CONTAINED, NON-REDUNDANT memories: keep a coherent task as ONE procedure, and create
        a separate NOTION only for a genuinely reusable fact. Do NOT split the same task across
        overlapping memories, do NOT create memories for trivial steps, and do NOT memorize generic
        GUI/OS mechanics (how to click an address bar, how browser autocomplete works): focus on what is
        SPECIFIC to the user's tools and task.

        Extract TWO kinds of memories:

        1) PROCEDURES (kind = "procedura"): how a task is carried out, step by step. They must be enough
           to reproduce the task using ONLY what you save (without re-watching the video). Include:
           - the applications/software used and how to open/reach them;
           - every step in the exact order, with the precise UI element (button/menu-item label,
             menu path, field name) and the action (click, scroll, keyboard shortcut);
           - the DATA entered/selected: exact values, source, format, meaning, and WHICH values vary
             at each run and how to obtain them;
           - shortcuts, checkpoints, preconditions, conditions ("if… then…") and exceptions.
           Prefer ROBUST, deterministic steps over fragile ones that depend on transient UI state
           (dropdown order, history/autocomplete suggestions). When the user reaches a website or
           resource via a shortcut or a suggestion, READ the canonical URL/address from the screen
           (e.g. the browser address bar after the page loads) and write the step as "open <that URL>";
           you may also note the user's shortcut as a habit, but the reproducible step is the URL.
           For password/credential fields, only note that the user enters their credentials — NEVER
           store credential values.

        2) NOTIONS (kind = "nozione"): general, reusable facts about the user and their environment,
           even if they are NOT steps of a procedure. For example: tools/apps used with their
           addresses/URLs and what they are for; where certain data lives; accounts, projects, naming,
           acronyms and their meaning; people/contacts; work conventions.
           Example: if the user opens Jira at https://jira.example to check tickets, BESIDES the
           procedure also save a notion like "The user uses Jira (https://jira.example) for ticket
           management" — so that for a future request like "check ticket 123" the agent knows where to
           go. ALWAYS try to extract the notions implicit in the session too.
           Always record addresses/URLs, account names and identifiers with their ACTUAL value as read
           from the screen — never a vague reference like "the corresponding URL".

        Capture every detail that is SPECIFIC and needed to reproduce the task (exact labels, values,
        URLs); skip what is generic or obvious GUI behavior.

        USER PROFILE (rare, optional): if the session reveals DURABLE information about the USER that
        rarely changes — their role/profession, the systems/tools they regularly use, stable
        preferences, long-term context — set "userProfileNote" to a short statement of it. In particular,
        if the user expresses or shows a PREFERENCE for specific software/apps for a given purpose (e.g.
        "I always use X for this", "open it in Y, not Z"), record that preferred software. Leave it null
        for anything ephemeral or specific to this single task. Most sessions leave it null.

        IMPORTANT: write "title", "summary" and "content" in the SAME language the user uses in the
        session (their speech / chat messages), e.g. Italian or English.

        Reply ONLY with a JSON object with an optional "userProfileNote" and the "memories" array:
        {
          "userProfileNote": null or "durable fact about the user (rare)",
          "memories": [
            {
              "kind": "procedura" or "nozione",
              "title": "short, descriptive title",
              "summary": "one-line summary",
              "content": "detailed content (possibly long, multi-line), in the user's language",
              "categoryPath": ["category", "subcategory"]
            }
          ]
        }
        For "categoryPath" use a path from general to specific and, when relevant, REUSE the existing
        categories you are given.
        The "title" must be SHORT and specific and must NOT repeat the path/categories (no "/" in the
        title): e.g. «Ricerca ticket non assegnati», NOT «Procedure / Jira / Ricerca ticket non
        assegnati» (those already live in categoryPath). No text outside the JSON.
        """;

    private readonly ILlmProvider _llm;
    private readonly IUserProfileMemory _profile;
    private readonly ILogger<TrainingAgent> _logger;

    public TrainingAgent(ILlmProvider llm, IUserProfileMemory profile, ILogger<TrainingAgent> logger)
    {
        _llm = llm;
        _profile = profile;
        _logger = logger;
    }

    public async Task<IReadOnlyList<LearnedKnowledge>> AnalyzeAsync(
        string videoPath,
        string eventTimeline,
        IReadOnlyList<string> existingCategories,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoPath);

        var video = await _llm.UploadFileAsync(videoPath, VideoMimeType, cancellationToken);

        var categories = existingCategories.Count > 0
            ? string.Join("\n", existingCategories.Select(c => "- " + c))
            : "(no categories yet)";

        var userText =
            $"Existing categories in memory (reuse them when relevant):\n{categories}\n\n"
            + $"Events and chat timeline:\n{eventTimeline}\n\n"
            + "Analyze the attached video and produce the required JSON.";

        var request = new LlmRequest
        {
            SystemInstruction = SystemPrompt,
            Messages = [new ChatMessage(ChatRole.User, userText)],
            Files = [video],
            Temperature = 0.2,
        };

        var response = await _llm.CompleteAsync(request, cancellationToken);

        // Se la sessione ha rivelato un fatto DUREVOLE sull'utente, aggiorniamo il profilo (merge
        // parsimonioso, best-effort): è indipendente dall'estrazione delle memorie.
        await RememberProfileNoteIfAnyAsync(response.Text, cancellationToken);

        return Parse(response.Text);
    }

    /// <summary>Se il JSON contiene una "userProfileNote", la unisce al profilo utente (best-effort).</summary>
    private async Task RememberProfileNoteIfAnyAsync(string responseText, CancellationToken cancellationToken)
    {
        try
        {
            var json = ExtractJsonObject(responseText);
            var note = JsonSerializer.Deserialize<KnowledgeListDto>(json, Json)?.UserProfileNote;
            if (!string.IsNullOrWhiteSpace(note))
                await _profile.RememberAsync(note, cancellationToken);
        }
        catch (JsonException)
        {
            // Il parsing "vero" delle memorie gestisce gli errori; qui restiamo best-effort.
        }
    }

    /// <summary>
    /// Interpreta il JSON del modello (array "memories"), tollerando recinti markdown e — come
    /// fallback — un singolo oggetto al posto dell'array. Restituisce una o più memorie.
    /// </summary>
    private IReadOnlyList<LearnedKnowledge> Parse(string responseText)
    {
        var json = ExtractJsonObject(responseText);

        List<KnowledgeDto>? items;
        try
        {
            items = JsonSerializer.Deserialize<KnowledgeListDto>(json, Json)?.Memories;

            // Fallback: alcuni modelli potrebbero restituire un singolo oggetto invece dell'array.
            if (items is null or { Count: 0 })
            {
                var single = JsonSerializer.Deserialize<KnowledgeDto>(json, Json);
                if (single is not null && !string.IsNullOrWhiteSpace(single.Content))
                    items = [single];
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON di conoscenza non interpretabile. Testo: {Text}", responseText);
            throw new LlmException("The model did not produce a parseable result.", ex);
        }

        var result = items?
            .Where(dto => !string.IsNullOrWhiteSpace(dto?.Content))
            .Select(dto => Map(dto!))
            .ToList();

        if (result is null or { Count: 0 })
            throw new LlmException("The model did not extract any knowledge from the session.");

        return result;
    }

    private static LearnedKnowledge Map(KnowledgeDto dto) => new(
        Title: string.IsNullOrWhiteSpace(dto.Title) ? "Attività appresa" : dto.Title.Trim(),
        Summary: dto.Summary?.Trim() ?? string.Empty,
        Content: dto.Content!.Trim(),
        CategoryPath: dto.CategoryPath is { Count: > 0 }
            ? dto.CategoryPath.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).ToList()
            : ["Generale"],
        Kind: string.Equals(dto.Kind?.Trim(), "procedura", StringComparison.OrdinalIgnoreCase)
            ? "procedura"
            : "nozione");

    /// <summary>Estrae la porzione JSON (dal primo '{' all'ultimo '}') dal testo del modello.</summary>
    private static string ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : text;
    }

    private sealed record KnowledgeListDto(List<KnowledgeDto>? Memories, string? UserProfileNote);

    private sealed record KnowledgeDto(string? Kind, string? Title, string? Summary, string? Content, List<string>? CategoryPath);
}
