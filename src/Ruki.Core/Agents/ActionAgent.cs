using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Ruki.Core.Abstractions;
using Ruki.Core.Automation;
using Ruki.Core.Capture;
using Ruki.Core.Llm;
using Ruki.Core.Memory;

namespace Ruki.Core.Agents;

/// <summary>
/// Implementazione dell'Action Agent. Mantiene una CONVERSAZIONE per tutta l'esecuzione: invia
/// l'obiettivo e l'albero di memoria (solo titoli) una volta, poi a ogni passo aggiunge solo
/// l'ultimo screenshot e la richiesta della prossima azione. Può navigare la memoria a richiesta
/// (espandere un nodo o leggere il contenuto di una memoria) ed eseguire azioni sul PC, fermandosi
/// quando il compito è concluso, fallisce, supera il limite di passi o l'utente lo interrompe.
/// </summary>
public sealed class ActionAgent : IActionAgent
{
    private static readonly TimeSpan StepDelay = TimeSpan.FromMilliseconds(700);

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private const string SystemPrompt =
        """
        You are Ruki's agent that PERFORMS tasks on the user's Windows PC by driving the mouse and
        keyboard. Proceed ONE STEP at a time: at each step you receive the latest screenshot of the
        screen (its size is given) and decide the NEXT single action based on what you see and on what
        has already happened in the conversation.

        When possible PREFER KEYBOARD SHORTCUTS (more reliable than the mouse), including Windows ones:
        Win+R (Run), Win+E (File Explorer), Win+D (desktop), Alt+Tab (switch window), Alt+F4 (close),
        Ctrl+L (browser address bar), etc.

        MEMORY: at the start you receive Ruki's memory tree with titles (and summaries) ONLY. To dig
        deeper, before acting, use:
        - "expand_node": see a node's children; requires "nodeId".
        - "read_memory": read the full content of one or more memories; requires "nodeIds" (list of ids).
        Expand/read only what you truly need for the task.

        The x and y coordinates are NORMALIZED from 0 to 1000 (x = horizontal position relative to the
        image width, y = vertical relative to the height), origin at the top-left. Example: screen
        center x=500 y=500; top-left corner x=0 y=0. ALWAYS give x and y in the 0–1000 range.

        At each step we tell you the current ACTIVE WINDOW. The full LIST of open windows is given only
        at the start: if you need it again, refreshed, ask for it with the "list_windows" action.

        Possible actions ("action" field):
        - "click", "double_click", "right_click": require x, y.
        - "type": type text; requires "text".
        - "key": press a key combination; "text" e.g. "Enter", "Ctrl+S", "Win+R".
        - "scroll": scroll; requires x, y and "amount" (positive = up, negative = down, in notches).
        - "wait": wait; "amount" in milliseconds.
        - "expand_node": "nodeId". "read_memory": "nodeIds". "list_windows": list of open windows.
        - "done": task completed; summary in "message".
        - "fail": cannot complete; explain in "message".

        For "type" and "key", set "window" to the window/app that must be in the foreground (title or
        app name): we will check the focus BEFORE sending the keys. If it isn't in the foreground, bring
        it there first.

        Click only on elements you SEE; do not invent coordinates. Aim for the element's GEOMETRIC CENTER
        on BOTH axes — halfway between its left and right edges (x) AND halfway between its top and bottom
        edges (y). A common mistake is getting x right but clicking too high or too low: estimate the
        element's full height and target its vertical middle, not its top edge or the text baseline. If a
        page needs to load, use "wait".

        If the screenshot contains a SMALLER nested copy of the desktop (e.g. a screen-recording or
        mirroring preview such as OBS, a "share screen" thumbnail, or a remote-desktop window), that copy
        is NOT the real UI — never click inside it; act on the real, full-size window instead.

        TEXT EDITING & CURSOR: a screenshot may NOT show the blinking text caret, so never assume where
        it is. When a "Text focus" note is provided (the focused field, the line the caret is on, and any
        selection), rely on it to know exactly where typing and Delete/Backspace will act. To edit or
        delete a SPECIFIC piece of text, first place the caret deterministically — click precisely on it,
        or SELECT the target (e.g. Home then Shift+End for a line, Shift+Down, or Ctrl+A) — and only then
        delete or type. Do NOT press Delete/Backspace hoping the caret is already on the right line.

        If an action is potentially DESTRUCTIVE or IRREVERSIBLE (delete, send/submit, pay, overwrite a
        file, close without saving, etc.), set "risky": true so the user can confirm it before it runs.

        VERIFY YOUR WORK (check, don't assume): set "expectation" to what should be visibly true on the
        screen right after the action (e.g. "the Replace dialog is open", "the To field shows the
        address"). At the NEXT step, BEFORE choosing a new action, look at the screenshot and confirm the
        previous expectation was met; if it was NOT met, do not push on — diagnose what went wrong and
        fix it. Be especially rigorous after CRITICAL steps (navigation, opening/closing dialogs,
        submitting, saving, deleting — anything the rest of the task depends on). Before "done", make sure
        the overall goal is actually achieved and visible on screen. Keep it proportionate: a quick glance
        for trivial steps, a real check for the critical ones.

        Write the user-facing "message" (for done/fail) in the SAME language as the goal.

        Reply ONLY with a JSON object, including only the relevant fields:
        { "thought": "...", "action": "...", "x": 0, "y": 0, "text": "..", "amount": 0, "window": "..",
          "nodeId": "..", "nodeIds": ["..."], "risky": false, "message": "..", "expectation": ".." }
        Keep "thought" to a single short line (no line breaks). No text outside the JSON.
        """;

    private readonly ILlmProvider _llm;
    private readonly IScreenCaptureService _screen;
    private readonly IInputAutomationService _input;
    private readonly IForegroundWindowService _foreground;
    private readonly ICaretContextProvider _caret;
    private readonly IClickIndicator _clickIndicator;
    private readonly IMemoryStore _memory;
    private readonly ISettingsService _settings;
    private readonly IActionTrace _trace;
    private readonly IActionConfirmation _confirmation;
    private readonly ILogger<ActionAgent> _logger;

    public ActionAgent(
        ILlmProvider llm,
        IScreenCaptureService screen,
        IInputAutomationService input,
        IForegroundWindowService foreground,
        ICaretContextProvider caret,
        IClickIndicator clickIndicator,
        IMemoryStore memory,
        ISettingsService settings,
        IActionTrace trace,
        IActionConfirmation confirmation,
        ILogger<ActionAgent> logger)
    {
        _llm = llm;
        _screen = screen;
        _input = input;
        _foreground = foreground;
        _caret = caret;
        _clickIndicator = clickIndicator;
        _memory = memory;
        _settings = settings;
        _trace = trace;
        _confirmation = confirmation;
        _logger = logger;
    }

    public async Task<ActionResult> RunAsync(string goal, IActionController controller)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goal);

        var token = controller.Token;
        var settings = _settings.Current;
        var maxSteps = settings.MaxActionSteps;

        // Conversazione: obiettivo + albero di memoria (solo titoli) una volta sola. Le frasi di
        // "impalcatura" inviate al modello sono in inglese, coerenti con il system prompt.
        var conversation = new List<ChatMessage>
        {
            new(ChatRole.User,
                $"Goal: {goal}\n\n"
                + "Ruki's memory (titles only; use expand_node/read_memory for details):\n"
                + RenderSkeleton(settings.MemoryTreeDepth)
                + "\n\n" + DescribeWindowContext()
                + DescribeCaret()
                + "\n\nHere is the latest screenshot of the screen; what is the next action?"),
        };

        // In debug mostriamo anche le ISTRUZIONI DI SISTEMA (non sono un turno della conversazione,
        // quindi altrimenti resterebbero invisibili nella finestra di debug).
        _trace.Add(new ActionTraceEntry(ActionTraceKind.Sent, "[System instructions to the agent]\n" + SystemPrompt, null, null, null, 0));

        await controller.WaitWhilePausedAsync(token);
        token.ThrowIfCancellationRequested();
        var screenshot = _screen.Capture();

        for (var step = 1; step <= maxSteps; step++)
        {
            await controller.WaitWhilePausedAsync(token);
            token.ThrowIfCancellationRequested();

            var action = await DecideAsync(conversation, screenshot, step, token);
            _logger.LogInformation("Azione {Step}: {Description}", step, action.Describe());

            switch (action.Type)
            {
                case AgentActionType.Done:
                    return new ActionResult(ActionOutcome.Completed, action.Message, step);
                case AgentActionType.Fail:
                    return new ActionResult(ActionOutcome.Failed, action.Message, step);

                case AgentActionType.ExpandNode:
                    conversation.Add(new ChatMessage(ChatRole.User,
                        $"Children of node {action.NodeId}:\n{ExpandNode(action.NodeId)}\n\nNext action?"));
                    continue;   // niente azione sul PC: lo schermo non cambia

                case AgentActionType.ReadMemory:
                    conversation.Add(new ChatMessage(ChatRole.User,
                        $"Memory content:\n{ReadMemories(action)}\n\nNext action?"));
                    continue;

                case AgentActionType.ListWindows:
                    conversation.Add(new ChatMessage(ChatRole.User,
                        $"{DescribeOpenWindows()}\n\nNext action?"));
                    continue;
            }

            // Azione rischiosa: se la conferma è attiva, chiediamo all'utente prima di eseguire.
            if (settings.ConfirmRiskyActions && action.Risky)
            {
                var approved = await _confirmation.ConfirmAsync(action.Describe(), token);
                if (!approved)
                {
                    _logger.LogInformation("Azione rischiosa rifiutata dall'utente: {Action}", action.Describe());
                    conversation.Add(new ChatMessage(ChatRole.User,
                        $"The user DECLINED this action ({action.Describe()}). "
                        + "Choose a different approach, or finish with done/fail."));
                    continue;   // niente azione sul PC: lo schermo non cambia
                }
            }

            // Azione sul PC.
            await controller.WaitWhilePausedAsync(token);
            token.ThrowIfCancellationRequested();
            var note = await ExecuteAsync(action, screenshot, token);

            await Task.Delay(StepDelay, token);   // pausa breve: l'utente segue e la UI si aggiorna

            // Lo schermo è cambiato: cattura il nuovo stato (e la finestra attiva) per il prossimo passo.
            screenshot = _screen.Capture();
            conversation.Add(new ChatMessage(ChatRole.User,
                $"Result: {note}.{DescribeExpectation(action)}\n{DescribeForeground()}{DescribeCaret()}\n"
                + "Here is the updated screen. First check whether the expected outcome actually happened; "
                + "if not, fix it. Then decide the next action."));
        }

        return new ActionResult(ActionOutcome.LimitReached, null, maxSteps);
    }

    /// <summary>Numero massimo di tentativi di interpretare la risposta di un passo prima di arrendersi.</summary>
    private const int MaxParseAttempts = 2;

    private async Task<AgentAction> DecideAsync(List<ChatMessage> conversation, CapturedFrame screenshot, int step, CancellationToken token)
    {
        for (var attempt = 1; ; attempt++)
        {
            var request = new LlmRequest
            {
                SystemInstruction = SystemPrompt,
                Messages = conversation.ToArray(),
                // Solo l'ultimo screenshot viene inviato (allegato all'ultimo turno utente).
                Images = [new LlmImage(screenshot.JpegBytes, "image/jpeg")],
                Temperature = 0.1,
            };

            _trace.Add(new ActionTraceEntry(ActionTraceKind.Sent, conversation[^1].Text, screenshot.JpegBytes, null, null, step));

            var response = await _llm.CompleteAsync(request, token);

            AgentAction action;
            try
            {
                // L'azione resta nelle coordinate del modello (normalizzate 0–1000): così le note che gli
                // rimandiamo parlano la sua stessa lingua. La conversione in pixel avviene solo dove serve
                // davvero (esecuzione del click e cerchio rosso del debug).
                action = Parse(response.Text);
            }
            catch (LlmException) when (attempt < MaxParseAttempts)
            {
                // Risposta non interpretabile (es. JSON malformato): invece di abortire l'intero compito,
                // chiediamo al modello di riformularla come JSON valido e riproviamo lo stesso passo.
                conversation.Add(new ChatMessage(ChatRole.Assistant, response.Text));
                conversation.Add(new ChatMessage(ChatRole.User,
                    "Your previous reply was not a single valid JSON object. Reply AGAIN with ONLY one valid "
                    + "JSON object for the next action, escaping any line breaks inside string values."));
                continue;
            }

            conversation.Add(new ChatMessage(ChatRole.Assistant, response.Text));
            // Il cerchio rosso del debug va disegnato in pixel sull'immagine catturata.
            _trace.Add(new ActionTraceEntry(ActionTraceKind.Received, response.Text, screenshot.JpegBytes,
                ToPixelX(action.X, screenshot), ToPixelY(action.Y, screenshot), step));
            return action;
        }
    }

    /// <summary>Esegue l'azione sul PC e restituisce la nota da mettere in conversazione (eseguita o saltata).</summary>
    private async Task<string> ExecuteAsync(AgentAction action, CapturedFrame frame, CancellationToken token)
    {
        // Per gli input da tastiera, verifica che la finestra attesa sia davvero in primo piano.
        if (action.Type is AgentActionType.Type or AgentActionType.Key && !string.IsNullOrWhiteSpace(action.Window))
        {
            var foreground = _foreground.GetForeground();
            if (!WindowMatches(action.Window, foreground))
            {
                var skipped = $"SKIPPED ({action.Describe()}) — expected window \"{action.Window}\" is not active "
                    + $"(foreground: {DescribeWindow(foreground)}). Bring focus to the correct window first.";
                _logger.LogWarning("{Skipped}", skipped);
                return skipped;
            }
        }

        // Le coordinate del modello (0–1000) diventano pixel reali solo qui, all'atto dell'esecuzione.
        var px = ToPixelX(action.X, frame) ?? 0;
        var py = ToPixelY(action.Y, frame) ?? 0;

        switch (action.Type)
        {
            case AgentActionType.Click:
                await ClickAsync(px, py, MouseButton.Left, doubleClick: false, token);
                break;
            case AgentActionType.DoubleClick:
                await ClickAsync(px, py, MouseButton.Left, doubleClick: true, token);
                break;
            case AgentActionType.RightClick:
                await ClickAsync(px, py, MouseButton.Right, doubleClick: false, token);
                break;
            case AgentActionType.Type:
                if (!string.IsNullOrEmpty(action.Text))
                    _input.TypeText(action.Text);
                break;
            case AgentActionType.Key:
                if (!string.IsNullOrWhiteSpace(action.Text))
                    _input.PressKeys(action.Text);
                break;
            case AgentActionType.Scroll:
                _input.Scroll(px, py, action.Amount ?? -3);
                break;
            case AgentActionType.Wait:
                await Task.Delay(Math.Clamp(action.Amount ?? 500, 0, 10_000), token);
                break;
        }

        return action.Describe();
    }

    /// <summary>Riempie l'anello ~0,25s prima del click e lo svuota ~0,25s dopo, per dare il tempo di seguire.</summary>
    private async Task ClickAsync(int x, int y, MouseButton button, bool doubleClick, CancellationToken token)
    {
        _clickIndicator.SetClicking(true);
        try
        {
            await Task.Delay(250, token);
            if (doubleClick)
                _input.DoubleClick(x, y, button);
            else
                _input.Click(x, y, button);
            await Task.Delay(250, token);
        }
        finally
        {
            _clickIndicator.SetClicking(false);
        }
    }

    // -------------------------------------------------------------- memoria

    /// <summary>Albero (solo titoli + riassunti + id) fino alla profondità indicata.</summary>
    private string RenderSkeleton(int maxDepth)
    {
        var builder = new StringBuilder();
        Walk(parentId: null, depth: 1, indent: string.Empty);
        return builder.Length == 0 ? "(empty memory)" : builder.ToString();

        void Walk(string? parentId, int depth, string indent)
        {
            foreach (var node in _memory.GetChildren(parentId))
            {
                if (node.IsObsolete)
                    continue;   // le memorie archiviate non vengono mostrate all'agente

                builder.AppendLine(indent + FormatNode(node, expandableHint: depth >= maxDepth));
                if (node.HasChildren && depth < maxDepth)
                    Walk(node.Id, depth + 1, indent + "  ");
            }
        }
    }

    private string ExpandNode(string? nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return "(no nodeId provided)";

        var children = _memory.GetChildren(nodeId).Where(c => !c.IsObsolete).ToList();
        if (children.Count == 0)
            return "(no children)";

        var builder = new StringBuilder();
        foreach (var node in children)
            builder.AppendLine(FormatNode(node, expandableHint: node.HasChildren));
        return builder.ToString();
    }

    private string ReadMemories(AgentAction action)
    {
        var ids = action.NodeIds ?? (action.NodeId is null ? [] : [action.NodeId]);
        if (ids.Count == 0)
            return "(no id provided)";

        var builder = new StringBuilder();
        foreach (var id in ids)
        {
            var node = _memory.GetNode(id);
            if (node is null)
            {
                builder.AppendLine($"[{id}] not found.");
                continue;
            }

            builder.AppendLine($"## {node.Title}");
            builder.AppendLine(string.IsNullOrWhiteSpace(node.Content) ? "(no content)" : node.Content);
            builder.AppendLine();
            _memory.TouchUsage(id);
        }

        return builder.ToString();
    }

    private static string FormatNode(MemoryNodeInfo node, bool expandableHint)
    {
        var kind = node.Type == MemoryNodeType.Category ? "cat" : "mem";
        var summary = string.IsNullOrWhiteSpace(node.Summary) ? string.Empty : $" — {node.Summary}";
        var more = expandableHint && node.HasChildren ? " [+]" : string.Empty;
        return $"[{node.Id}] ({kind}) {node.Title}{summary}{more}";
    }

    // -------------------------------------------------------------- finestra

    private static bool WindowMatches(string expected, ForegroundWindowInfo? info)
    {
        if (info is null)
            return false;

        var target = expected.Trim();
        return info.Title.Contains(target, StringComparison.OrdinalIgnoreCase)
            || info.ProcessName.Contains(target, StringComparison.OrdinalIgnoreCase)
            || (info.ProcessName.Length > 0 && target.Contains(info.ProcessName, StringComparison.OrdinalIgnoreCase));
    }

    private static string DescribeWindow(ForegroundWindowInfo? info)
        => info is null ? "none" : $"\"{info.ProcessName} — {info.Title}\"";

    /// <summary>Solo la finestra attiva corrente (inviata a ogni passo).</summary>
    private string DescribeForeground() => $"Active window: {DescribeWindow(_foreground.GetForeground())}.";

    /// <summary>Promemoria di cosa il modello si aspettava dopo l'azione, da verificare al passo successivo.</summary>
    private static string DescribeExpectation(AgentAction action)
        => string.IsNullOrWhiteSpace(action.Expectation) ? string.Empty : $" Expected outcome: \"{action.Expectation}\".";

    /// <summary>
    /// Evidenza (best-effort) di dove si trova il caret di testo: campo a fuoco, riga corrente,
    /// selezione. Inviata insieme allo screenshot perché il caret spesso non è visibile nell'immagine.
    /// Riga a sé; stringa vuota se non disponibile (l'app non espone il testo via accessibilità).
    /// </summary>
    private string DescribeCaret()
    {
        var caret = _caret.Describe();
        return string.IsNullOrWhiteSpace(caret) ? string.Empty : "\n" + caret;
    }

    /// <summary>Elenco delle finestre aperte (inviato solo all'inizio o su richiesta con list_windows).</summary>
    private string DescribeOpenWindows()
    {
        var windows = _foreground.GetOpenWindows();
        var list = windows.Count == 0
            ? "(none)"
            : string.Join("; ", windows.Take(15).Select(w => $"{w.ProcessName} — {w.Title}"));
        return $"Open windows: {list}.";
    }

    /// <summary>Contesto completo (attiva + elenco), usato solo nel primo turno.</summary>
    private string DescribeWindowContext() => $"{DescribeForeground()} {DescribeOpenWindows()}";

    /// <summary>Converte una X normalizzata (0–1000) restituita dal modello in pixel sulla larghezza del frame.</summary>
    private static int? ToPixelX(int? normalized, CapturedFrame frame)
        => normalized is { } nx ? (int)Math.Round(Math.Clamp(nx, 0, 1000) / 1000.0 * frame.Width) : null;

    /// <summary>Converte una Y normalizzata (0–1000) restituita dal modello in pixel sull'altezza del frame.</summary>
    private static int? ToPixelY(int? normalized, CapturedFrame frame)
        => normalized is { } ny ? (int)Math.Round(Math.Clamp(ny, 0, 1000) / 1000.0 * frame.Height) : null;

    // -------------------------------------------------------------- parsing

    private AgentAction Parse(string responseText)
    {
        // I modelli scrivono spesso "thought" su più righe: ripariamo gli a capo grezzi nelle stringhe
        // prima di deserializzare, così un campo multilinea non fa fallire (e abortire) l'intera azione.
        var json = JsonText.RepairControlChars(ExtractJsonObject(responseText));
        ActionDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<ActionDto>(json, Json);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Azione non interpretabile. Testo: {Text}", responseText);
            throw new LlmException("The model did not produce a parseable action.", ex);
        }

        var type = MapType(dto?.Action);
        return new AgentAction(type, dto?.X, dto?.Y, dto?.Text, dto?.Amount,
            dto?.Message, dto?.Window, dto?.NodeId, dto?.NodeIds, dto?.Risky ?? false, dto?.Expectation);
    }

    private static AgentActionType MapType(string? action) => action?.ToLowerInvariant() switch
    {
        "click" => AgentActionType.Click,
        "double_click" or "doubleclick" => AgentActionType.DoubleClick,
        "right_click" or "rightclick" => AgentActionType.RightClick,
        "type" => AgentActionType.Type,
        "key" => AgentActionType.Key,
        "scroll" => AgentActionType.Scroll,
        "wait" => AgentActionType.Wait,
        "expand_node" or "expandnode" => AgentActionType.ExpandNode,
        "read_memory" or "readmemory" => AgentActionType.ReadMemory,
        "list_windows" or "listwindows" => AgentActionType.ListWindows,
        "done" => AgentActionType.Done,
        _ => AgentActionType.Fail,
    };

    private static string ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : text;
    }

    private sealed record ActionDto(
        string? Action, int? X, int? Y, string? Text, int? Amount, string? Message,
        string? Window, string? NodeId, List<string>? NodeIds, bool? Risky, string? Thought, string? Expectation);
}
