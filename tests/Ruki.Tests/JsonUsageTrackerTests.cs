using Microsoft.Extensions.Logging.Abstractions;
using Ruki.Infrastructure.Usage;
using Xunit;

namespace Ruki.Tests;

/// <summary>Test del conteggio token mensile (accumulo e reset al cambio di mese).</summary>
public class JsonUsageTrackerTests
{
    [Fact]
    public void Record_AccumulatesInputAndOutputSeparately()
    {
        var file = TempFile();
        try
        {
            var tracker = new JsonUsageTracker(NullLogger<JsonUsageTracker>.Instance, file);

            tracker.Record(100, 40);
            tracker.Record(50, 10);

            Assert.Equal(150, tracker.Current.InputTokens);
            Assert.Equal(50, tracker.Current.OutputTokens);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Current_ResetsWhenStoredMonthIsOld()
    {
        var file = TempFile();
        try
        {
            // Conteggio di un mese passato: alla lettura deve azzerarsi.
            File.WriteAllText(file, "{\"Month\":\"2000-01\",\"InputTokens\":999,\"OutputTokens\":888}");
            var tracker = new JsonUsageTracker(NullLogger<JsonUsageTracker>.Instance, file);

            Assert.Equal(0, tracker.Current.InputTokens);
            Assert.Equal(0, tracker.Current.OutputTokens);
        }
        finally { File.Delete(file); }
    }

    private static string TempFile() => Path.Combine(Path.GetTempPath(), $"ruki-usage-{Guid.NewGuid():N}.json");
}
