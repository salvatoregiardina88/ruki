using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ruki.Core.Abstractions;
using Ruki.Infrastructure.Storage;

namespace Ruki.Infrastructure.Usage;

/// <summary>
/// Conteggio dei token mensili persistito in <c>usage.json</c>. Il file memorizza anche il mese a
/// cui il conteggio si riferisce: quando cambia mese, il conteggio riparte da zero (reset lazy, senza
/// timer). Le scritture sono atomiche e serializzate con un lock.
/// </summary>
public sealed class JsonUsageTracker : IUsageTracker
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly object _gate = new();
    private readonly ILogger<JsonUsageTracker> _logger;
    private readonly string _file;
    private UsageState _state;

    /// <param name="usageFilePath">In produzione <c>null</c> (percorso predefinito); nei test un file temporaneo.</param>
    public JsonUsageTracker(ILogger<JsonUsageTracker> logger, string? usageFilePath = null)
    {
        _logger = logger;
        _file = usageFilePath ?? RukiPaths.UsageFile;
        _state = Load();
    }

    public event EventHandler<UsageSnapshot>? Changed;

    public UsageSnapshot Current
    {
        get
        {
            lock (_gate)
            {
                EnsureCurrentMonth();
                return new UsageSnapshot(_state.InputTokens, _state.OutputTokens);
            }
        }
    }

    public void Record(long inputTokens, long outputTokens)
    {
        if (inputTokens <= 0 && outputTokens <= 0)
            return;

        UsageSnapshot snapshot;
        lock (_gate)
        {
            EnsureCurrentMonth();
            _state.InputTokens += inputTokens;
            _state.OutputTokens += outputTokens;
            Persist();
            snapshot = new UsageSnapshot(_state.InputTokens, _state.OutputTokens);
        }

        Changed?.Invoke(this, snapshot);
    }

    /// <summary>Se il mese memorizzato non è quello corrente, azzera il conteggio. Da chiamare sotto lock.</summary>
    private void EnsureCurrentMonth()
    {
        var month = CurrentMonth();
        if (_state.Month == month)
            return;

        _state = new UsageState { Month = month };
        Persist();
    }

    private static string CurrentMonth() => DateTimeOffset.Now.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    private UsageState Load()
    {
        try
        {
            if (File.Exists(_file))
            {
                var loaded = JsonSerializer.Deserialize<UsageState>(File.ReadAllText(_file));
                if (loaded is not null)
                    return loaded;
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _logger.LogWarning(ex, "File usage.json illeggibile: riparto da zero.");
        }

        return new UsageState { Month = CurrentMonth() };
    }

    private void Persist()
    {
        try
        {
            AtomicFile.WriteAllText(_file, JsonSerializer.Serialize(_state, JsonOptions));
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Salvataggio di usage.json fallito.");
        }
    }

    private sealed class UsageState
    {
        public string Month { get; set; } = string.Empty;
        public long InputTokens { get; set; }
        public long OutputTokens { get; set; }
    }
}
