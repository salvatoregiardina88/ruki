using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Ruki.App.Localization;
using Ruki.App.Services;
using Ruki.Core.Agents;
using Ruki.Core.Llm;
using Ruki.Core.Training;

namespace Ruki.App.ViewModels;

/// <summary>
/// ViewModel della finestra di chat. Fa da ponte tra la UI e l'<see cref="IOrchestratorAgent"/>:
/// raccoglie l'input, mostra i messaggi e gestisce stato "occupato" ed errori.
/// </summary>
public sealed partial class ChatViewModel : ObservableObject
{
    private readonly IOrchestratorAgent _orchestrator;
    private readonly ITrainingSessionRecorder _recorder;
    private readonly ActionSession _action;
    private readonly ILogger<ChatViewModel> _logger;

    /// <summary>Messaggi mostrati a schermo (l'orchestratore tiene a parte la cronologia per il modello).</summary>
    public ObservableCollection<ChatBubble> Messages { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _input = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private bool _isBusy;

    public ChatViewModel(
        IOrchestratorAgent orchestrator,
        ITrainingSessionRecorder recorder,
        ActionSession action,
        ILogger<ChatViewModel> logger)
    {
        _orchestrator = orchestrator;
        _recorder = recorder;
        _action = action;
        _logger = logger;

        // L'esito di un'azione eseguita sul PC va mostrato come risposta in chat (non solo in overlay).
        _action.OutcomeReported += OnActionOutcome;

        // Ricostruisce la conversazione visibile: benvenuto + eventuali turni già avvenuti
        // (utile se la finestra viene riaperta nella stessa sessione).
        Messages.Add(ChatBubble.Assistant(_orchestrator.WelcomeMessage));
        foreach (var message in _orchestrator.History)
        {
            Messages.Add(message.Role == ChatRole.User
                ? ChatBubble.User(message.Text)
                : ChatBubble.Assistant(message.Text));
        }
    }

    /// <summary>Esito di un'azione: lo aggiungiamo come messaggio dell'assistente (già sul thread UI).</summary>
    private void OnActionOutcome(object? sender, string message)
        => Messages.Add(ChatBubble.Assistant(message));

    /// <summary>Da chiamare alla chiusura della finestra per non lasciare l'iscrizione appesa.</summary>
    public void Detach() => _action.OutcomeReported -= OnActionOutcome;

    private bool CanSend() => !IsBusy && !string.IsNullOrWhiteSpace(Input);

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var text = Input.Trim();
        Input = string.Empty;
        Messages.Add(ChatBubble.User(text));

        // Se è in corso una sessione di addestramento, il messaggio entra nella timeline.
        _recorder.NoteChatMessage(ChatRole.User, text);

        IsBusy = true;
        try
        {
            var reply = await _orchestrator.SendAsync(text);
            Messages.Add(ChatBubble.Assistant(reply.Text));
            _recorder.NoteChatMessage(ChatRole.Assistant, reply.Text);

            // Se l'orchestratore ha riconosciuto la richiesta di un compito, lo eseguiamo sul PC.
            if (!string.IsNullOrWhiteSpace(reply.ActionGoal))
            {
                Messages.Add(ChatBubble.Assistant(Loc.T("Chat_Executing", reply.ActionGoal)));
                _action.Start(reply.ActionGoal);
            }

            // Aggiorna il profilo in memoria, in background. L'orchestratore decide SE rifarlo davvero
            // (solo dopo abbastanza nuovi messaggi) e lo UNISCE al profilo esistente, senza sovrascriverlo.
            _ = UpdateProfileInBackgroundAsync();
        }
        catch (LlmException ex)
        {
            // Errore "atteso" e comunicabile (chiave mancante, modello errato, blocco…).
            Messages.Add(ChatBubble.Error(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore imprevisto durante l'invio del messaggio.");
            Messages.Add(ChatBubble.Error(Loc.T("Chat_UnexpectedError", ex.Message)));
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Aggiorna il profilo utente senza disturbare la chat: gli errori vengono solo loggati.</summary>
    private async Task UpdateProfileInBackgroundAsync()
    {
        try
        {
            await _orchestrator.UpdateUserProfileAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Aggiornamento del profilo utente non riuscito.");
        }
    }
}
