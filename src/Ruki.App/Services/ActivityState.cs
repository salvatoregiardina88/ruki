using Ruki.Core.Abstractions;
using Ruki.Core.Training;

namespace Ruki.App.Services;

/// <summary>
/// Determina lo stato corrente di Ruki dalla registrazione di addestramento e dalla sessione
/// d'azione, così l'orchestratore sa sempre se siamo in addestramento, in esecuzione o in chat.
/// </summary>
public sealed class ActivityState : IActivityState
{
    private readonly ITrainingSessionRecorder _recorder;
    private readonly ActionSession _action;

    public ActivityState(ITrainingSessionRecorder recorder, ActionSession action)
    {
        _recorder = recorder;
        _action = action;
    }

    public RukiActivity Current =>
        _recorder.IsRecording ? RukiActivity.Training
        : _action.IsRunning ? RukiActivity.Executing
        : RukiActivity.Idle;
}
