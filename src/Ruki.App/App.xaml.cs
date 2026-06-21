using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ruki.App.Services;
using Ruki.App.ViewModels;
using Ruki.App.Views;
using Ruki.Core.Abstractions;
using Ruki.Core.DependencyInjection;
using Ruki.Core.Training;
using Ruki.Infrastructure.DependencyInjection;
using Ruki.Infrastructure.Storage;
using Serilog;

namespace Ruki.App;

/// <summary>
/// Punto di ingresso dell'applicazione WPF.
/// <para>
/// Si occupa del "bootstrap": prepara le cartelle dati, configura il logging (Serilog),
/// costruisce il container di dependency injection (Generic Host) e mostra l'overlay.
/// Tutto il resto dell'app riceve i propri servizi dal container, non li crea a mano.
/// </para>
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    // Tenuto vivo per tutta la durata dell'app: garantisce una sola istanza di Ruki.
    private Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Una sola istanza: due overlay con due set di hook globali si darebbero fastidio a vicenda.
        if (!TryAcquireSingleInstance())
        {
            MessageBox.Show("Ruki è già in esecuzione.", "Ruki", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        try
        {
            // 1. Cartelle dati e logging devono essere pronti prima di tutto il resto.
            RukiPaths.EnsureCreated();
            ConfigureSerilog();

            // 2. Intercetta le eccezioni non gestite che arrivano al thread della UI,
            //    così un bug in un handler non chiude l'app in modo silenzioso.
            DispatcherUnhandledException += OnDispatcherUnhandledException;

            // 3. Costruisci e avvia il container.
            _host = BuildHost();
            _host.Start();

            // 3b. Applica la lingua scelta (stringhe UI + cultura) prima di creare le finestre.
            var settings = _host.Services.GetRequiredService<Ruki.Core.Abstractions.ISettingsService>();
            Localization.LanguageManager.Apply(settings.Current.UiLanguage);

            // 4. Mostra l'overlay (la finestra "principale" di Ruki) e, all'avvio, anche la chat
            //    (posizionata sopra l'overlay una volta che questo ha calcolato la sua posizione).
            var overlay = _host.Services.GetRequiredService<OverlayWindow>();
            MainWindow = overlay;
            var windows = _host.Services.GetRequiredService<IWindowService>();
            overlay.Loaded += (_, _) => windows.ShowChat();
            overlay.Show();

            // 5. Pulizia delle sessioni vecchie in background (non blocca l'avvio).
            var cleaner = _host.Services.GetRequiredService<ISessionCleaner>();
            Task.Run(cleaner.CleanupOldSessions);

            // 6. Verifica aggiornamenti in background: silenziosa, chiede solo se c'è una novità.
            if (settings.Current.CheckForUpdatesOnStartup)
                _ = CheckForUpdatesAsync();

            Log.Information("Ruki avviato correttamente.");
        }
        catch (Exception ex)
        {
            // Se il bootstrap fallisce non possiamo fare molto: logghiamo e usciamo
            // mostrando un messaggio comprensibile invece di un crash muto.
            Log.Fatal(ex, "Avvio di Ruki fallito.");
            MessageBox.Show(
                $"Avvio di Ruki fallito:\n\n{ex.Message}",
                "Ruki", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Se si chiude durante una registrazione, fermiamola in modo pulito (rilascia gli hook,
        // chiude i file) prima di smontare il container.
        StopRecordingIfRunning();

        // Arresto pulito del container e flush dei log su disco.
        if (_host is not null)
        {
            _host.StopAsync().GetAwaiter().GetResult();
            _host.Dispose();
        }

        _singleInstanceMutex?.Dispose();

        Log.Information("Ruki terminato.");
        Log.CloseAndFlush();

        base.OnExit(e);
    }

    /// <summary>Acquisisce il mutex di istanza singola. False se un'altra istanza è già in esecuzione.</summary>
    private bool TryAcquireSingleInstance()
    {
        // "Local\" = per sessione utente: due utenti diversi possono comunque avere ciascuno la sua Ruki.
        _singleInstanceMutex = new Mutex(initiallyOwned: true, @"Local\Ruki_SingleInstance", out var isNew);
        if (isNew)
            return true;

        _singleInstanceMutex.Dispose();
        _singleInstanceMutex = null;
        return false;
    }

    /// <summary>Ferma una registrazione eventualmente in corso, senza codificare il video (chiusura rapida).</summary>
    private void StopRecordingIfRunning()
    {
        if (_host is null)
            return;

        try
        {
            var recorder = _host.Services.GetRequiredService<Ruki.Core.Training.ITrainingSessionRecorder>();
            if (recorder.IsRecording)
            {
                Log.Information("Chiusura durante una registrazione: la interrompo in modo pulito.");
                recorder.Abort();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Arresto della registrazione alla chiusura non riuscito.");
        }
    }

    /// <summary>
    /// Configura il logger globale Serilog: console di debug + file giornalieri con
    /// rotazione, conservati per due settimane.
    /// </summary>
    private static void ConfigureSerilog()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.Debug()
            .WriteTo.File(
                Path.Combine(RukiPaths.LogsDirectory, "ruki-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    /// <summary>
    /// Crea il Generic Host con tutti i servizi registrati: infrastruttura, servizi UI,
    /// ViewModel e finestre.
    /// </summary>
    private static IHost BuildHost()
    {
        var builder = Host.CreateApplicationBuilder();

        // Logging: instradiamo Microsoft.Extensions.Logging verso Serilog.
        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(dispose: false);

        // Servizi di base (impostazioni, segreti, provider Gemini, e in futuro cattura/memoria…).
        builder.Services.AddRukiInfrastructure();

        // Agenti del dominio (orchestratore, training, memoria, azione).
        builder.Services.AddRukiCore();

        // Manutenzione periodica della memoria (deduplica) in background.
        builder.Services.AddHostedService<MemoryMaintenanceScheduler>();

        // Servizi della UI.
        builder.Services.AddSingleton<IWindowService, WindowService>();
        builder.Services.AddSingleton<Ruki.Core.Automation.IClickIndicator, ClickIndicator>();
        builder.Services.AddSingleton<Ruki.Core.Agents.IActionConfirmation, DialogActionConfirmation>();
        builder.Services.AddSingleton<Ruki.Core.Capture.IPasswordFieldDetector, UiaPasswordFieldDetector>();
        builder.Services.AddSingleton<ActionSession>();
        builder.Services.AddSingleton<Ruki.Core.Abstractions.IActivityState, ActivityState>();

        // ViewModel.
        builder.Services.AddSingleton<OverlayViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<MemoryViewModel>();
        builder.Services.AddTransient<ChatViewModel>();
        builder.Services.AddTransient<ActionDebugViewModel>();

        // Finestre. L'overlay è singleton (ne esiste una sola); le altre sono transitorie
        // e il WindowService garantisce a runtime una sola istanza aperta per tipo.
        builder.Services.AddSingleton<OverlayWindow>();
        builder.Services.AddTransient<SettingsWindow>();
        builder.Services.AddTransient<ChatWindow>();
        builder.Services.AddTransient<ActionDebugWindow>();

        return builder.Build();
    }

    /// <summary>
    /// Verifica in background la presenza di un aggiornamento e, se c'è, chiede all'utente se
    /// aprirne la pagina. Silenziosa: in caso di errore o nessuna novità non mostra nulla.
    /// </summary>
    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var update = await _host!.Services.GetRequiredService<IUpdateChecker>().CheckForUpdateAsync();
            if (update is not null)
                await Dispatcher.InvokeAsync(() => PromptUpdate(update));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Verifica aggiornamenti fallita.");
        }
    }

    private static void PromptUpdate(UpdateInfo update)
    {
        var choice = MessageBox.Show(
            Localization.Loc.T("Update_Body", update.Version),
            Localization.Loc.T("Update_Title"),
            MessageBoxButton.YesNo, MessageBoxImage.Information);

        if (choice != MessageBoxResult.Yes)
            return;

        try
        {
            // Apre la pagina della release nel browser predefinito (UseShellExecute).
            Process.Start(new ProcessStartInfo(update.Url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Apertura della pagina di aggiornamento fallita.");
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Eccezione non gestita nel thread della UI.");
        MessageBox.Show(
            $"Si è verificato un errore imprevisto:\n\n{e.Exception.Message}",
            "Ruki", MessageBoxButton.OK, MessageBoxImage.Warning);

        // Segniamo l'eccezione come gestita per evitare la chiusura immediata dell'app
        // su un errore non fatale. I crash veri restano comunque tracciati nel log.
        e.Handled = true;
    }
}
