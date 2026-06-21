using Microsoft.Extensions.DependencyInjection;
using Ruki.Core.Agents;

namespace Ruki.Core.DependencyInjection;

/// <summary>
/// Registra i servizi del dominio (gli agenti). L'orchestratore è singleton perché la sua
/// conversazione deve sopravvivere alla chiusura/riapertura della finestra di chat, ma non
/// oltre la sessione dell'app.
/// </summary>
public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddRukiCore(this IServiceCollection services)
    {
        services.AddSingleton<IOrchestratorAgent, OrchestratorAgent>();
        services.AddSingleton<ITrainingAgent, TrainingAgent>();
        services.AddSingleton<IMemoryAgent, MemoryAgent>();
        services.AddSingleton<IMemoryMaintenanceAgent, MemoryMaintenanceAgent>();
        services.AddSingleton<IActionAgent, ActionAgent>();
        services.AddSingleton<IActionTrace, ActionTrace>();
        return services;
    }
}
