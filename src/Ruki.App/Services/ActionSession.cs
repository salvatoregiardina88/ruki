using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Ruki.App.Localization;
using Ruki.Core.Abstractions;
using Ruki.Core.Agents;
using Ruki.Core.Automation;

namespace Ruki.App.Services;

/// <summary>
/// Coordina l'esecuzione di un compito da parte dell'Action Agent: prepara il contesto dalla memoria,
/// crea il controller (pausa/stop), installa l'hotkey globale di stop, lancia l'agente in background
/// ed espone lo stato (in esecuzione / in pausa / messaggio) alla UI.
/// </summary>
public sealed partial class ActionSession : ObservableObject
{
    private readonly IActionAgent _agent;
    private readonly IGlobalStopHotkey _hotkey;
    private readonly IClickIndicator _clickIndicator;
    private readonly ISettingsService _settings;
    private readonly IActionTrace _trace;
    private readonly IWindowService _windows;
    private readonly ILogger<ActionSession> _logger;

    private ActionController? _controller;

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _isPaused;
    [ObservableProperty] private string _statusText = string.Empty;

    public ActionSession(
        IActionAgent agent,
        IGlobalStopHotkey hotkey,
        IClickIndicator clickIndicator,
        ISettingsService settings,
        IActionTrace trace,
        IWindowService windows,
        ILogger<ActionSession> logger)
    {
        _agent = agent;
        _hotkey = hotkey;
        _clickIndicator = clickIndicator;
        _settings = settings;
        _trace = trace;
        _windows = windows;
        _logger = logger;
    }

    /// <summary>Avvia l'esecuzione del compito. Va chiamato dal thread UI (installa l'hotkey).</summary>
    public void Start(string goal)
    {
        if (IsRunning || string.IsNullOrWhiteSpace(goal))
            return;

        var controller = new ActionController();
        _controller = controller;

        // Modalità debug: traccia la conversazione e apri la finestra di debug a destra.
        var debug = _settings.Current.DebugMode;
        _trace.Enabled = debug;
        if (debug)
        {
            _windows.ShowActionDebug();
            _trace.Clear();
        }

        _hotkey.Start(Stop);          // Esc ferma l'esecuzione (installato sul thread UI)
        _clickIndicator.Start();      // anello che segue il cursore per tutta l'esecuzione
        IsRunning = true;
        IsPaused = false;
        StatusText = Loc.T("Action_Running", goal);

        // L'agente gira su un thread in background: muove mouse/tastiera senza bloccare la UI.
        Task.Run(() => RunAsync(goal, controller));
    }

    public void TogglePause()
    {
        if (_controller is null)
            return;

        if (_controller.IsPaused)
        {
            _controller.Resume();
            IsPaused = false;
            StatusText = Loc.T("Action_Resumed");
        }
        else
        {
            _controller.Pause();
            IsPaused = true;
            StatusText = Loc.T("Action_Paused");
        }
    }

    public void Stop() => _controller?.Stop();

    private async Task RunAsync(string goal, ActionController controller)
    {
        try
        {
            var result = await _agent.RunAsync(goal, controller);
            SetStatusOnUi(DescribeResult(result));
        }
        catch (OperationCanceledException)
        {
            SetStatusOnUi(Loc.T("Action_Interrupted"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Esecuzione del compito fallita.");
            SetStatusOnUi(Loc.T("Action_Error", ex.Message));
        }
        finally
        {
            OnUi(() =>
            {
                _hotkey.Stop();
                _clickIndicator.Stop();   // l'anello scompare a fine esecuzione (qualunque sia l'esito)
                IsRunning = false;
                IsPaused = false;
            });
            controller.Dispose();
            _controller = null;
        }
    }

    /// <summary>
    /// Compone il messaggio di esito localizzato dall'<see cref="ActionResult"/>. Il dettaglio del
    /// modello (se presente) è già nella lingua dell'obiettivo; i casi deterministici (limite passi,
    /// esito senza dettaglio) li traduciamo qui nella lingua dell'interfaccia.
    /// </summary>
    private static string DescribeResult(ActionResult result) => result.Outcome switch
    {
        ActionOutcome.LimitReached => Loc.T("Action_MaxSteps", result.Steps),
        ActionOutcome.Completed => string.IsNullOrWhiteSpace(result.Detail)
            ? Loc.T("Action_DoneNoDetail")
            : Loc.T("Action_Done", result.Detail),
        _ => string.IsNullOrWhiteSpace(result.Detail)
            ? Loc.T("Action_FailedNoDetail")
            : Loc.T("Action_NotDone", result.Detail),
    };

    private void SetStatusOnUi(string text) => OnUi(() => StatusText = text);

    private static void OnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
            action();
        else
            dispatcher.Invoke(action);
    }
}
