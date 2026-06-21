using Ruki.Core.Configuration;
using Xunit;

namespace Ruki.Tests;

/// <summary>Test della logica pura di <see cref="RukiSettings"/> (normalizzazione e clone).</summary>
public class RukiSettingsTests
{
    [Fact]
    public void Normalize_ClampsValuesIntoValidRanges()
    {
        var settings = new RukiSettings
        {
            ScreenCaptureFps = 999,        // troppo alto
            MaxSessionMinutes = 0,         // troppo basso
            MemoryMaintenanceIntervalHours = -5,
        };

        settings.Normalize();

        Assert.Equal(10.0, settings.ScreenCaptureFps);     // limite massimo
        Assert.Equal(1, settings.MaxSessionMinutes);       // limite minimo
        Assert.Equal(1, settings.MemoryMaintenanceIntervalHours);
    }

    [Fact]
    public void Normalize_RestoresDefaultModelWhenBlank()
    {
        var settings = new RukiSettings { GeminiModel = "   " };

        settings.Normalize();

        Assert.False(string.IsNullOrWhiteSpace(settings.GeminiModel));
    }

    [Fact]
    public void Clone_ProducesIndependentCopy()
    {
        var original = new RukiSettings { GeminiModel = "modello-a", MaxSessionMinutes = 5 };

        var copy = original.Clone();
        copy.GeminiModel = "modello-b";
        copy.MaxSessionMinutes = 42;

        // Modificare la copia non deve toccare l'originale.
        Assert.Equal("modello-a", original.GeminiModel);
        Assert.Equal(5, original.MaxSessionMinutes);
    }
}
