using Microsoft.Extensions.Logging.Abstractions;
using Ruki.Core.Configuration;
using Ruki.Infrastructure.Storage;
using Xunit;

namespace Ruki.Tests;

/// <summary>
/// Test di <see cref="JsonSettingsService"/>. Ogni test usa un file temporaneo isolato,
/// così non tocca mai le impostazioni reali dell'utente.
/// </summary>
public sealed class JsonSettingsServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _settingsFile;

    public JsonSettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ruki-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _settingsFile = Path.Combine(_tempDir, "settings.json");
    }

    private JsonSettingsService CreateService()
        => new(NullLogger<JsonSettingsService>.Instance, _settingsFile);

    [Fact]
    public void Load_WhenFileMissing_CreatesDefaults()
    {
        var service = CreateService();

        Assert.True(File.Exists(_settingsFile));      // i default vengono persistiti
        Assert.Equal(10, service.Current.MaxSessionMinutes);
    }

    [Fact]
    public void Save_PersistsAndUpdatesCurrent()
    {
        var service = CreateService();

        var updated = service.Current.Clone();
        updated.GeminiModel = "modello-test";
        updated.MaxSessionMinutes = 7;
        service.Save(updated);

        // Una nuova istanza deve rileggere i valori salvati dal file.
        var reloaded = CreateService();
        Assert.Equal("modello-test", reloaded.Current.GeminiModel);
        Assert.Equal(7, reloaded.Current.MaxSessionMinutes);
    }

    [Fact]
    public void Save_RaisesChangedEvent()
    {
        var service = CreateService();
        RukiSettings? received = null;
        service.Changed += (_, s) => received = s;

        var updated = service.Current.Clone();
        updated.GeminiModel = "nuovo";
        service.Save(updated);

        Assert.NotNull(received);
        Assert.Equal("nuovo", received!.GeminiModel);
    }

    [Fact]
    public void Save_NormalizesBeforePersisting()
    {
        var service = CreateService();

        var updated = service.Current.Clone();
        updated.MaxSessionMinutes = 9999;     // fuori range
        service.Save(updated);

        Assert.Equal(60, service.Current.MaxSessionMinutes);  // limato al massimo
    }

    [Fact]
    public void Load_WhenFileCorrupt_FallsBackToDefaultsAndBacksUp()
    {
        File.WriteAllText(_settingsFile, "{ questo non è json valido ");

        var service = CreateService();

        Assert.Equal(10, service.Current.MaxSessionMinutes);          // default
        Assert.True(File.Exists(_settingsFile + ".corrupt"));         // backup creato
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch (IOException) { /* best effort: cartella temporanea */ }
    }
}
