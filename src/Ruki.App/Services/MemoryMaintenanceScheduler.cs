using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ruki.Core.Abstractions;
using Ruki.Core.Agents;

namespace Ruki.App.Services;

/// <summary>
/// Esegue periodicamente la manutenzione della memoria (deduplica), con la frequenza configurata.
/// Il primo ciclo parte dopo un intervallo completo, così non sorprende l'utente all'avvio.
/// </summary>
public sealed class MemoryMaintenanceScheduler : IHostedService, IDisposable
{
    private readonly IMemoryMaintenanceAgent _agent;
    private readonly ISettingsService _settings;
    private readonly ILogger<MemoryMaintenanceScheduler> _logger;
    private Timer? _timer;

    public MemoryMaintenanceScheduler(
        IMemoryMaintenanceAgent agent,
        ISettingsService settings,
        ILogger<MemoryMaintenanceScheduler> logger)
    {
        _agent = agent;
        _settings = settings;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var interval = TimeSpan.FromHours(Math.Max(1, _settings.Current.MemoryMaintenanceIntervalHours));
        _timer = new Timer(_ => _ = RunSafelyAsync(), null, interval, interval);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        return Task.CompletedTask;
    }

    private async Task RunSafelyAsync()
    {
        try
        {
            await _agent.RunAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Manutenzione periodica della memoria fallita.");
        }
    }

    public void Dispose() => _timer?.Dispose();
}
