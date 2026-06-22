using Microsoft.Extensions.DependencyInjection;
using Ruki.Core.Abstractions;
using Ruki.Core.Automation;
using Ruki.Core.Capture;
using Ruki.Core.Llm;
using Ruki.Core.Memory;
using Ruki.Core.Training;
using Ruki.Infrastructure.Automation;
using Ruki.Infrastructure.Capture;
using Ruki.Infrastructure.Llm.Gemini;
using Ruki.Infrastructure.Sessions;
using Ruki.Infrastructure.Startup;
using Ruki.Infrastructure.Storage;
using Ruki.Infrastructure.Update;
using Ruki.Infrastructure.Usage;

namespace Ruki.Infrastructure.DependencyInjection;

/// <summary>
/// Estensioni per registrare i servizi dell'infrastruttura nel container DI.
/// Tiene la composizione (chi implementa cosa) dentro l'infrastruttura, così la UI
/// chiama un solo metodo e non deve conoscere le classi concrete.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registra archivi su disco (impostazioni e segreti) e prepara le cartelle dati.
    /// Man mano che il progetto cresce, qui si aggiungono gli altri servizi
    /// infrastrutturali (Gemini, cattura, memoria, automazione…).
    /// </summary>
    public static IServiceCollection AddRukiInfrastructure(this IServiceCollection services)
    {
        // Le cartelle devono esistere prima che qualunque servizio scriva su disco.
        RukiPaths.EnsureCreated();

        services.AddSingleton<ISecretStore, DpapiSecretStore>();
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<IMemoryStore, SqliteMemoryStore>();

        // Codifica video (frame + audio -> MP4) tramite l'encoder integrato in Windows.
        services.AddSingleton<IVideoEncoder, MediaFoundationVideoEncoder>();

        // Provider del modello AI (Gemini). Usa un HttpClient con timeout ampio: le chiamate
        // a un LLM possono richiedere svariati secondi.
        services.AddHttpClient("gemini", client => client.Timeout = TimeSpan.FromMinutes(3));
        services.AddSingleton<ILlmProvider, GeminiProvider>();

        // Conteggio dei token consumati nel mese (per la stima dei costi mostrata nell'overlay).
        services.AddSingleton<IUsageTracker, JsonUsageTracker>();

        // Cattura della sessione di addestramento (schermo, audio, eventi) e relativa regia.
        services.AddSingleton<IScreenCaptureService, GdiScreenCaptureService>();
        services.AddSingleton<IAudioRecorder, NAudioRecorder>();
        services.AddSingleton<IInputEventSource, Win32InputEventSource>();
        services.AddSingleton<ITrainingSessionRecorder, TrainingSessionRecorder>();
        services.AddSingleton<ISessionCleaner, SessionCleaner>();
        services.AddSingleton<ITrainingPipeline, TrainingPipeline>();

        // Verifica aggiornamenti tramite GitHub Releases (timeout breve: non rallenta l'avvio).
        services.AddHttpClient("github", client => client.Timeout = TimeSpan.FromSeconds(10));
        services.AddSingleton<IUpdateChecker, GitHubUpdateChecker>();

        // Avvio automatico con Windows (chiave di registro per-utente).
        services.AddSingleton<IStartupManager, RegistryStartupManager>();

        // Esecuzione dei task sul PC (Action Agent): automazione input e scorciatoie globali (stop/pausa).
        services.AddSingleton<IInputAutomationService, Win32InputAutomationService>();
        services.AddSingleton<IForegroundWindowService, Win32ForegroundWindowService>();
        services.AddSingleton<IGlobalActionHotkeys, GlobalActionHotkeys>();

        return services;
    }
}
