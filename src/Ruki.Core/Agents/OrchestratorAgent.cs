using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ruki.Core.Abstractions;
using Ruki.Core.Configuration;
using Ruki.Core.Llm;
using Ruki.Core.Memory;

namespace Ruki.Core.Agents;

/// <summary>
/// Implementazione dell'agente orchestratore. Mantiene la cronologia in memoria e, a ogni
/// messaggio, chiama il modello passando l'istruzione di sistema e l'intera conversazione.
/// Legge dalla memoria il profilo dell'utente per ricordarlo tra le sessioni.
/// </summary>
public sealed class OrchestratorAgent : IOrchestratorAgent
{
    // Istruzione di sistema di base (in inglese: i modelli seguono meglio le istruzioni in inglese,
    // ma rispondono nella lingua dell'utente). Definisce chi è Ruki e come si comporta l'orchestratore.
    private const string BaseSystemPrompt =
        """
        You are Ruki, a personal AI assistant living as a small app on the user's Windows PC.
        Your role is the "orchestrator": you converse with the user in a natural, concise way.

        ALWAYS reply in the SAME language the user used in their latest message (e.g. Italian or
        English). Do not switch language on your own.

        What Ruki can do:
        - Learn tasks the user demonstrates in dedicated "teaching" sessions (recording screen, audio
          and actions).
        - Later perform those tasks by driving the mouse and keyboard, under the user's supervision.
        - Remember useful information about the user and their work.

        You have already greeted the user and asked them to introduce themselves and describe their
        work: use their answers to understand how you can help.

        Guidelines:
        - Friendly, direct tone. Be concise: a few sentences, unless an explanation is needed.
        - If the user wants to teach you something, explain they can press the «Insegna» button.
        - If the user asks you to PERFORM a task on their computer (e.g. "go check ticket 123",
          "open X and do Y"), besides replying, set "actionGoal" to the goal to execute, phrased
          clearly and self-contained. Otherwise leave "actionGoal" as null.
        - Do not invent capabilities you don't have; if something isn't possible, say so honestly.

        LONG-TERM MEMORY about the user: if (and ONLY if) the user shares DURABLE information that rarely
        changes and is worth remembering across sessions — their role/profession, the systems and tools
        they regularly use, stable preferences, long-term context — set "profileNote" to a short statement
        of that fact, in the user's language. For ordinary or EPHEMERAL things (what they are doing today,
        transient details, small talk), leave "profileNote" null. Only tell the user you have remembered or
        saved something if you actually set "profileNote" — never claim to remember it without setting it.

        Reply ALWAYS and ONLY with a JSON object:
        { "reply": "your message to the user, in the user's language", "actionGoal": null or "goal to execute",
          "profileNote": null or "durable fact about the user to remember" }
        No text outside the JSON.
        """;

    private readonly List<ChatMessage> _history = [];

    private readonly ILlmProvider _llm;
    private readonly IUserProfileMemory _profile;
    private readonly ISecretStore _secrets;
    private readonly IActivityState _activity;
    private readonly ILogger<OrchestratorAgent> _logger;

    public OrchestratorAgent(
        ILlmProvider llm,
        IUserProfileMemory profile,
        ISecretStore secrets,
        IActivityState activity,
        ILogger<OrchestratorAgent> logger)
    {
        _llm = llm;
        _profile = profile;
        _secrets = secrets;
        _activity = activity;
        _logger = logger;
    }

    /// <summary>
    /// Messaggio di benvenuto mostrato all'apertura della chat. Cambia in base a tre fattori:
    /// se manca la chiave API (primo passo da fare), se l'utente è già conosciuto (esiste un
    /// profilo) e la lingua dell'interfaccia. Il testo è scelto qui perché viene mostrato PRIMA
    /// che l'utente scriva: per le risposte successive è il modello ad adattarsi alla lingua usata.
    /// </summary>
    public string WelcomeMessage
    {
        get
        {
            var english = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
                .Equals("en", StringComparison.OrdinalIgnoreCase);

            // Senza chiave API non si può fare nulla: spieghiamo cos'è l'app e il primo passo.
            if (!_secrets.Has(SecretKeys.GeminiApiKey))
                return english
                    ? "Hi, I'm Ruki 👋 I learn the tasks you show me and then do them for you, on your "
                      + "computer, under your supervision.\n\n"
                      + "To get started you'll need a **Google Gemini API key**: open «Settings» (the gear "
                      + "at the top right) and paste it there. Usage is pay-as-you-go (there's an initial "
                      + "free tier). To work, Ruki sends your messages, screenshots, audio and memories to "
                      + "Google Gemini — please check its privacy and pricing information.\n\n"
                      + "Once that's done, come back here and tell me about yourself!"
                    : "Ciao, sono Ruki 👋 imparo le attività che mi mostri e poi le eseguo al posto tuo, "
                      + "sul tuo computer, sotto il tuo controllo.\n\n"
                      + "Per iniziare serve una **chiave API di Google Gemini**: aprila in «Impostazioni» "
                      + "(l'ingranaggio in alto a destra) e incollala lì. L'uso è a consumo (c'è un piano "
                      + "gratuito iniziale). Per funzionare Ruki invia a Google Gemini i tuoi messaggi, gli "
                      + "screenshot, l'audio e le memorie: dai un'occhiata alle sue informative su privacy e costi.\n\n"
                      + "Fatto questo, torna qui e raccontami di te!";

            // Solo un profilo ATTIVO ci fa dire "ci conosciamo": se è archiviato, è come non averlo.
            var known = !string.IsNullOrWhiteSpace(_profile.GetActiveProfile());

            return (english, known) switch
            {
                // Italiano — primo avvio.
                (false, false) =>
                    "Ciao, sono Ruki 👋 il tuo assistente personale.\n\n" +
                    "Tante cose al computer le so già fare da solo: aprire programmi, cercare sul web, " +
                    "sbrigare piccole faccende. Ma è sul *tuo* lavoro che divento davvero utile — " +
                    "mostrami una volta come fai una cosa (basta premere «Insegna») e da lì in poi me " +
                    "la ricordo e la faccio io per te, mentre tu controlli.\n\n" +
                    "Per partire con il piede giusto, raccontami un po' di te: di cosa ti occupi e in " +
                    "cosa potrei darti una mano?",

                // Italiano — utente già conosciuto.
                (false, true) =>
                    "Bentornato 👋 mi ricordo di te e di come lavori.\n\n" +
                    "Dimmi pure di cosa hai bisogno e ci penso io. Se invece c'è qualcosa di nuovo da " +
                    "imparare, premi «Insegna» e mostramelo: lo aggiungo a ciò che so già fare per te.",

                // Inglese — primo avvio.
                (true, false) =>
                    "Hi, I'm Ruki 👋 your personal assistant.\n\n" +
                    "I can already handle plenty of everyday things on the computer: opening apps, " +
                    "searching the web, small chores. But it's with *your* work that I become truly " +
                    "useful — show me once how you do something (just press the «Teach» button) and " +
                    "from then on I'll remember it and do it for you, while you keep an eye on things.\n\n" +
                    "To start off on the right foot, tell me a bit about yourself: what do you do, and " +
                    "how could I lend a hand?",

                // Inglese — utente già conosciuto.
                (true, true) =>
                    "Welcome back 👋 I remember you and how you work.\n\n" +
                    "Just tell me what you need and I'll take care of it. If there's something new to " +
                    "learn, press the «Teach» button and show me: I'll add it to what I can already " +
                    "do for you.",
            };
        }
    }

    public IReadOnlyList<ChatMessage> History => _history;

    public async Task<OrchestratorReply> SendAsync(string userMessage, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);

        // Aggiungiamo subito il turno utente alla cronologia.
        _history.Add(new ChatMessage(ChatRole.User, userMessage));

        try
        {
            var request = new LlmRequest
            {
                SystemInstruction = BuildSystemInstruction(),
                Messages = _history.ToArray(),
                Temperature = 0.7,
            };

            var response = await _llm.CompleteAsync(request, cancellationToken);
            var reply = ParseReply(response.Text);
            // In cronologia teniamo il testo "pulito" per l'utente, non il JSON grezzo.
            _history.Add(new ChatMessage(ChatRole.Assistant, reply.Text));

            // Se l'orchestratore ha DECISO di ricordare un fatto durevole sull'utente, lo scriviamo
            // davvero in memoria (merge parsimonioso), in background per non rallentare la risposta.
            if (!string.IsNullOrWhiteSpace(reply.ProfileNote))
                _ = _profile.RememberAsync(reply.ProfileNote);

            return reply;
        }
        catch
        {
            // Se la chiamata fallisce rimuoviamo il turno utente appena aggiunto, così la
            // cronologia resta coerente e un eventuale nuovo tentativo non la "sporca".
            _history.RemoveAt(_history.Count - 1);
            _logger.LogWarning("Invio messaggio all'orchestratore fallito.");
            throw;
        }
    }

    public void NoteActionOutcome(string outcome)
    {
        if (string.IsNullOrWhiteSpace(outcome))
            return;

        // Lo registriamo come turno dell'assistente: per il modello è il "report" del risultato.
        // Eventuali turni assistente consecutivi vengono fusi a valle, nel payload per Gemini.
        _history.Add(new ChatMessage(ChatRole.Assistant, outcome.Trim()));
    }

    public void Reset()
    {
        _history.Clear();
        _logger.LogInformation("Conversazione dell'orchestratore azzerata.");
    }

    /// <summary>Istruzione di sistema = prompt di base + stato corrente + profilo noto (se attivo).</summary>
    private string BuildSystemInstruction()
    {
        var instruction = BaseSystemPrompt + "\n\n" + DescribeState();

        var profile = _profile.GetActiveProfile();
        if (!string.IsNullOrWhiteSpace(profile))
            instruction += "\n\nKnown information about the user (from Ruki's memory):\n" + profile;

        return instruction;
    }

    /// <summary>Descrive all'orchestratore lo stato corrente (chat/addestramento/esecuzione) e come comportarsi.</summary>
    private string DescribeState() => _activity.Current switch
    {
        RukiActivity.Training =>
            "CURRENT STATE: a TEACHING session is being RECORDED right now — the user is demonstrating a "
            + "task to teach you, and your chat messages are part of that recording. Be encouraging and "
            + "concise; do NOT set an actionGoal now.",
        RukiActivity.Executing =>
            "CURRENT STATE: a task is currently being EXECUTED on the PC by the action agent. Do NOT start "
            + "another task (leave actionGoal null); you may comment on the one in progress.",
        _ =>
            "CURRENT STATE: normal chat — no teaching session and no task running.",
    };

    /// <summary>Estrae risposta + eventuale obiettivo + eventuale nota profilo dal JSON; in fallback, testo semplice.</summary>
    private static OrchestratorReply ParseReply(string responseText)
    {
        // Ripara gli a capo grezzi nelle stringhe (le risposte di chat sono spesso multilinea):
        // evita che un JSON tecnicamente non valido finisca nel fallback testo-grezzo.
        var json = JsonText.RepairControlChars(ExtractJsonObject(responseText));
        try
        {
            var dto = JsonSerializer.Deserialize<ReplyDto>(json, JsonOptions);
            if (dto is not null && !string.IsNullOrWhiteSpace(dto.Reply))
                return new OrchestratorReply(
                    dto.Reply.Trim(),
                    string.IsNullOrWhiteSpace(dto.ActionGoal) ? null : dto.ActionGoal.Trim(),
                    string.IsNullOrWhiteSpace(dto.ProfileNote) ? null : dto.ProfileNote.Trim());
        }
        catch (JsonException)
        {
            // Modello che non ha usato il JSON: usiamo il testo così com'è (fallback sotto).
        }

        return new OrchestratorReply(responseText.Trim(), null);
    }

    private static string ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : text;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private sealed record ReplyDto(string? Reply, string? ActionGoal, string? ProfileNote);
}
