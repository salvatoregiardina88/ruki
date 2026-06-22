using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Ruki.App.Localization;
using Ruki.App.Services;
using Ruki.Core.Abstractions;
using Ruki.Core.Training;

namespace Ruki.App.ViewModels;

/// <summary>
/// ViewModel dell'overlay. Espone i comandi dei pulsanti (Chat, Impostazioni, Chiudi) e gestisce
/// direttamente l'insegnamento: il pulsante "Insegna" avvia/ferma la registrazione senza aprire
/// finestre, mostrando solo un pallino rosso e un piccolo cronometro nell'overlay stesso.
/// </summary>
public sealed partial class OverlayViewModel : ObservableObject
{
    private readonly IWindowService _windows;
    private readonly ITrainingSessionRecorder _recorder;
    private readonly ISessionCleaner _cleaner;
    private readonly ITrainingPipeline _pipeline;
    private readonly IUsageTracker _usage;
    private readonly ISettingsService _settings;
    private readonly ILogger<OverlayViewModel> _logger;
    private readonly DispatcherTimer _uiTimer;

    /// <summary>Stato dell'esecuzione di un compito sul PC (Action Agent), per i controlli in overlay.</summary>
    public ActionSession Action { get; }

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private string _elapsedText = "00:00";

    /// <summary>True dopo la soglia di durata massima: usato per colorare il cronometro (avviso non bloccante).</summary>
    [ObservableProperty]
    private bool _maxReached;

    /// <summary>Microfono in muto durante la registrazione (la UI mostra il microfono barrato).</summary>
    [ObservableProperty]
    private bool _isMicMuted;

    /// <summary>
    /// UNICA riga di stato dell'overlay: ci scrivono sia l'apprendimento (es. "Sto imparando…",
    /// "Imparato: …") sia l'esito breve delle azioni ("Fatto"/"Non riuscito"). L'ultimo messaggio
    /// rimpiazza il precedente, così non restano mai due messaggi sovrapposti.
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>Riga piccola in basso: token I/O del mese e stima costo (es. "Token I/O: 500k/392k ~ 3€").</summary>
    [ObservableProperty]
    private string _usageText = string.Empty;

    public OverlayViewModel(
        IWindowService windows,
        ITrainingSessionRecorder recorder,
        ISessionCleaner cleaner,
        ITrainingPipeline pipeline,
        IUsageTracker usage,
        ISettingsService settings,
        ActionSession action,
        ILogger<OverlayViewModel> logger)
    {
        _windows = windows;
        _recorder = recorder;
        _cleaner = cleaner;
        _pipeline = pipeline;
        _usage = usage;
        _settings = settings;
        Action = action;
        _logger = logger;

        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _uiTimer.Tick += (_, _) => ElapsedText = Format(_recorder.Elapsed);

        // Lo stato (breve) delle azioni confluisce nell'unica riga di stato dell'overlay: l'ultimo
        // messaggio vince, sostituendo un eventuale messaggio di apprendimento ancora visibile.
        Action.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ActionSession.StatusText) && !string.IsNullOrEmpty(Action.StatusText))
                StatusMessage = Action.StatusText;
        };

        // Aggiorna la stima token/costo quando arrivano nuovi consumi o cambiano le tariffe.
        _usage.Changed += (_, _) => RefreshUsageOnUi();
        _settings.Changed += (_, _) => RefreshUsageOnUi();
        UpdateUsageText();
    }

    [RelayCommand]
    private void OpenChat() => _windows.ShowChat();

    [RelayCommand]
    private void OpenSettings() => _windows.ShowSettings();

    [RelayCommand]
    private void Exit() => Application.Current.Shutdown();

    /// <summary>Muta/riattiva il microfono durante la registrazione.</summary>
    [RelayCommand]
    private void ToggleMic()
    {
        IsMicMuted = !IsMicMuted;
        _recorder.IsMicMuted = IsMicMuted;
    }

    [RelayCommand]
    private void TogglePauseAction() => Action.TogglePause();

    [RelayCommand]
    private void StopAction() => Action.Stop();

    [RelayCommand]
    private async Task ToggleTrainingAsync()
    {
        if (!_recorder.IsRecording)
            StartTraining();
        else
            await StopTrainingAsync();
    }

    /// <summary>
    /// Annulla l'insegnamento in corso: ferma la registrazione, NON invia nulla all'apprendimento
    /// ed elimina tutti i dati della sessione registrati finora.
    /// </summary>
    [RelayCommand]
    private void CancelTraining()
    {
        if (!_recorder.IsRecording)
            return;

        _uiTimer.Stop();
        _recorder.MaxDurationReached -= OnMaxDurationReached;
        _recorder.Discard();

        IsRecording = false;
        MaxReached = false;
        IsMicMuted = false;
        ElapsedText = "00:00";
        StatusMessage = Loc.T("Learn_Discarded");
    }

    private void StartTraining()
    {
        MaxReached = false;
        IsMicMuted = false;   // ogni sessione parte col microfono acceso
        StatusMessage = string.Empty;
        _recorder.MaxDurationReached += OnMaxDurationReached;
        _recorder.Start();

        IsRecording = true;
        ElapsedText = "00:00";
        _uiTimer.Start();
    }

    private async Task StopTrainingAsync()
    {
        _uiTimer.Stop();

        // Usciamo subito dallo stato "registrazione": niente più pallino rosso né cronometro.
        // Durante la creazione del video mostriamo un messaggio di attesa al suo posto.
        _recorder.MaxDurationReached -= OnMaxDurationReached;
        IsRecording = false;
        MaxReached = false;
        ElapsedText = "00:00";
        var reorganizing = Loc.T("Learn_Reorganizing");
        StatusMessage = reorganizing;

        TrainingSessionInfo? session = null;
        try
        {
            session = await _recorder.StopAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Arresto della registrazione fallito.");
            StatusMessage = Loc.T("Learn_SessionCloseError");
        }

        // Tiene il disco sotto controllo eliminando le sessioni più vecchie.
        try
        {
            _cleaner.CleanupOldSessions();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pulizia delle sessioni fallita.");
        }

        // Apprendimento in background: non deve bloccare l'overlay (può durare a lungo).
        if (session?.VideoPath is not null)
            _ = RunLearningPipelineAsync(session);
        else if (StatusMessage == reorganizing)
            StatusMessage = string.Empty;   // niente video da apprendere (e nessun errore prima)
    }

    private async Task RunLearningPipelineAsync(TrainingSessionInfo session)
    {
        StatusMessage = Loc.T("Learn_Learning");
        try
        {
            var count = await _pipeline.ProcessSessionAsync(session);
            // Solo un'informazione generica: il numero di memorie create (niente dettagli).
            StatusMessage = count == 1 ? Loc.T("Learn_LearnedOne") : Loc.T("Learn_LearnedMany", count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Apprendimento dalla sessione fallito.");
            StatusMessage = Loc.T("Learn_Failed");
        }
    }

    private void OnMaxDurationReached()
        => Application.Current.Dispatcher.Invoke(() => MaxReached = true);

    private static string Format(TimeSpan elapsed) => $"{(int)elapsed.TotalMinutes:D2}:{elapsed.Seconds:D2}";

    /// <summary>Il tracker notifica da un thread in background: aggiorniamo sul thread UI.</summary>
    private void RefreshUsageOnUi()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
            UpdateUsageText();
        else
            dispatcher.InvokeAsync(UpdateUsageText);
    }

    private void UpdateUsageText()
    {
        var usage = _usage.Current;
        var s = _settings.Current;
        var cost = usage.InputTokens / 1_000_000.0 * s.TokenPriceInputPerMillion
                 + usage.OutputTokens / 1_000_000.0 * s.TokenPriceOutputPerMillion;

        UsageText = $"Token I/O: {FormatTokens(usage.InputTokens)}/{FormatTokens(usage.OutputTokens)} ~ {cost:0.00}{s.CostCurrencySymbol}";
    }

    /// <summary>Token in forma compatta: 950, 500k, 1.5M.</summary>
    private static string FormatTokens(long n) => n switch
    {
        >= 1_000_000 => $"{n / 1_000_000.0:0.#}M",
        >= 1_000 => $"{n / 1_000.0:0.#}k",
        _ => n.ToString(),
    };
}
