namespace Ruki.Core.Agents;

/// <summary>
/// Implementazione di <see cref="IActionController"/>: combina un <see cref="CancellationTokenSource"/>
/// (per lo Stop) con un evento per la pausa.
/// </summary>
public sealed class ActionController : IActionController, IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly ManualResetEventSlim _notPaused = new(initialState: true);

    public CancellationToken Token => _cts.Token;

    public bool IsPaused => !_notPaused.IsSet;

    public void Pause() => _notPaused.Reset();

    public void Resume() => _notPaused.Set();

    public void Stop()
    {
        _notPaused.Set();   // sblocca un'eventuale attesa di pausa, così lo Stop ha effetto subito
        _cts.Cancel();
    }

    public async Task WaitWhilePausedAsync(CancellationToken cancellationToken)
    {
        while (!_notPaused.IsSet)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(100, cancellationToken);
        }
    }

    public void Dispose()
    {
        _cts.Dispose();
        _notPaused.Dispose();
    }
}
