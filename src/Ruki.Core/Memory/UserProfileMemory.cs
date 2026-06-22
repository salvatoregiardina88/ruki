using Microsoft.Extensions.Logging;
using Ruki.Core.Llm;

namespace Ruki.Core.Memory;

/// <summary>
/// Implementazione di <see cref="IUserProfileMemory"/>: tiene il nodo "Profilo utente" e, quando un
/// agente chiede di ricordare un fatto, chiede al modello di UNIRLO al profilo esistente in modo
/// parsimonioso — solo informazioni che cambiano di rado, mai contesto effimero.
/// </summary>
public sealed class UserProfileMemory : IUserProfileMemory
{
    /// <summary>Titolo del nodo radice in cui vive il profilo (escluso dalla manutenzione memoria).</summary>
    public const string ProfileNodeTitle = "Profilo utente";

    // Istruzione di MERGE: integra un nuovo fatto durevole preservando il profilo; scarta l'effimero.
    private const string MergePrompt =
        """
        You maintain a SMALL, STABLE profile of the user for Ruki's long-term memory: ONLY facts that
        rarely change — their role/profession, the systems and tools they regularly use, stable
        preferences, durable context. NEVER store ephemeral or one-off context (what they are doing
        today, temporary states, transient task details).

        You are given the CURRENT profile (it may be empty) and a NEW fact. Output the UPDATED profile:
        - If the new fact is durable and worth keeping, integrate it: PRESERVE everything still valid,
          and merge/deduplicate (refine or correct an existing point instead of duplicating it).
        - If the new fact is ephemeral or not worth long-term memory, output the CURRENT profile UNCHANGED.
        - Be concise: short bullet points, in the user's language. Output ONLY the profile text.
        """;

    private readonly ILlmProvider _llm;
    private readonly IMemoryStore _memory;
    private readonly ILogger<UserProfileMemory> _logger;

    private bool _running;   // evita merge concorrenti (le note possono arrivare ravvicinate)

    public UserProfileMemory(ILlmProvider llm, IMemoryStore memory, ILogger<UserProfileMemory> logger)
    {
        _llm = llm;
        _memory = memory;
        _logger = logger;
    }

    public string? GetActiveProfile()
    {
        // Solo se ATTIVO (non archiviato): se l'utente l'ha disattivato è come non averlo.
        var node = GetProfileNode();
        return node is { IsObsolete: false } && !string.IsNullOrWhiteSpace(node.Content) ? node.Content : null;
    }

    public async Task RememberAsync(string durableFact, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(durableFact) || _running)
            return;

        _running = true;
        try
        {
            var existing = GetProfileNode()?.Content;
            var request = new LlmRequest
            {
                SystemInstruction = MergePrompt,
                Messages =
                [
                    new ChatMessage(ChatRole.User,
                        "CURRENT profile (may be empty):\n"
                        + (string.IsNullOrWhiteSpace(existing) ? "(none)" : existing)
                        + "\n\nNEW fact to remember:\n" + durableFact.Trim()),
                ],
                Temperature = 0.2,
            };

            var updated = (await _llm.CompleteAsync(request, cancellationToken)).Text;
            if (string.IsNullOrWhiteSpace(updated))
                return;

            UpsertProfile(updated.Trim());
            _logger.LogInformation("Profilo utente aggiornato (merge di una nota durevole).");
        }
        catch (Exception ex)
        {
            // Best-effort: un fallimento qui non deve disturbare la chat né l'apprendimento.
            _logger.LogWarning(ex, "Aggiornamento del profilo utente non riuscito.");
        }
        finally
        {
            _running = false;
        }
    }

    /// <summary>Crea o aggiorna il nodo "Profilo utente" (preserva l'eventuale stato di archiviazione).</summary>
    private void UpsertProfile(string profile)
    {
        var existing = GetProfileNode();
        if (existing is not null)
        {
            existing.Content = profile;
            existing.Summary = FirstLine(profile);
            _memory.Update(existing);
        }
        else
        {
            _memory.Add(new MemoryNode
            {
                Title = ProfileNodeTitle,
                Type = MemoryNodeType.Memory,
                Summary = FirstLine(profile),
                Content = profile,
            });
        }
    }

    /// <summary>Cerca il nodo "Profilo utente" tra i nodi radice e ne carica il contenuto completo.</summary>
    private MemoryNode? GetProfileNode()
    {
        var info = _memory.GetChildren(null)
            .FirstOrDefault(n => string.Equals(n.Title, ProfileNodeTitle, StringComparison.OrdinalIgnoreCase));
        return info is null ? null : _memory.GetNode(info.Id);
    }

    private static string FirstLine(string text)
    {
        var line = text.Split('\n', 2)[0].Trim();
        return line.Length <= 120 ? line : line[..120] + "…";
    }
}
