namespace Ruki.Core.Agents;

/// <summary>Implementazione di <see cref="IActionTrace"/> (singleton, semplice event aggregator).</summary>
public sealed class ActionTrace : IActionTrace
{
    public bool Enabled { get; set; }

    public event Action<ActionTraceEntry>? EntryAdded;
    public event Action? Cleared;

    public void Clear() => Cleared?.Invoke();

    public void Add(ActionTraceEntry entry)
    {
        if (Enabled)
            EntryAdded?.Invoke(entry);
    }
}
