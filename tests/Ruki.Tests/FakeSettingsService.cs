using Ruki.Core.Abstractions;
using Ruki.Core.Configuration;

namespace Ruki.Tests;

/// <summary>Settings finto per i test: restituisce le impostazioni fornite (o i default).</summary>
internal sealed class FakeSettingsService : ISettingsService
{
    public FakeSettingsService(RukiSettings? settings = null) => Current = settings ?? new RukiSettings();

    public RukiSettings Current { get; }

    public RukiSettings Load() => Current;

    public void Save(RukiSettings settings) { }

    public event EventHandler<RukiSettings>? Changed { add { } remove { } }
}
