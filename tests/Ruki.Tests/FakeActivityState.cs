using Ruki.Core.Abstractions;

namespace Ruki.Tests;

/// <summary>Stato di attività finto per i test (di default: chat normale).</summary>
internal sealed class FakeActivityState(RukiActivity current = RukiActivity.Idle) : IActivityState
{
    public RukiActivity Current { get; } = current;
}
