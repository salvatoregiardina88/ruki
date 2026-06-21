using Ruki.Core.Abstractions;
using Ruki.Core.Configuration;

namespace Ruki.Tests;

/// <summary>Secret store finto in memoria per i test (niente DPAPI, niente disco).</summary>
internal sealed class FakeSecretStore : ISecretStore
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    public FakeSecretStore(bool withGeminiKey = false)
    {
        if (withGeminiKey)
            _values[SecretKeys.GeminiApiKey] = "test-key";
    }

    public void Set(string key, string secret) => _values[key] = secret;
    public string? Get(string key) => _values.TryGetValue(key, out var v) ? v : null;
    public bool Has(string key) => _values.ContainsKey(key);
    public void Delete(string key) => _values.Remove(key);
}
