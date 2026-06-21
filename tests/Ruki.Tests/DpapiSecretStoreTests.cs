using Microsoft.Extensions.Logging.Abstractions;
using Ruki.Infrastructure.Storage;
using Xunit;

namespace Ruki.Tests;

/// <summary>
/// Test di <see cref="DpapiSecretStore"/>. Usa una cartella temporanea isolata.
/// La cifratura DPAPI è reale (lega i dati all'utente Windows che esegue i test).
/// </summary>
public sealed class DpapiSecretStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly DpapiSecretStore _store;

    public DpapiSecretStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ruki-tests", Guid.NewGuid().ToString("N"));
        _store = new DpapiSecretStore(NullLogger<DpapiSecretStore>.Instance, _tempDir);
    }

    [Fact]
    public void Set_Then_Get_RoundTripsTheSecret()
    {
        _store.Set("api", "valore-super-segreto-123");

        Assert.Equal("valore-super-segreto-123", _store.Get("api"));
    }

    [Fact]
    public void Get_WhenMissing_ReturnsNull()
    {
        Assert.Null(_store.Get("non-esiste"));
    }

    [Fact]
    public void Has_ReflectsPresence()
    {
        Assert.False(_store.Has("k"));
        _store.Set("k", "x");
        Assert.True(_store.Has("k"));
    }

    [Fact]
    public void Delete_RemovesTheSecret()
    {
        _store.Set("k", "x");
        _store.Delete("k");

        Assert.False(_store.Has("k"));
        Assert.Null(_store.Get("k"));
    }

    [Fact]
    public void Set_OverwritesExistingValue()
    {
        _store.Set("k", "primo");
        _store.Set("k", "secondo");

        Assert.Equal("secondo", _store.Get("k"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch (IOException) { /* best effort: cartella temporanea */ }
    }
}
