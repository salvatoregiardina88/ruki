using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Ruki.App.Localization;
using Ruki.Core.Abstractions;
using Ruki.Core.Configuration;
using Ruki.Core.Llm;

namespace Ruki.App.ViewModels;

/// <summary>
/// ViewModel della finestra Impostazioni (tab "API").
/// <para>
/// Carica i valori correnti da <see cref="ISettingsService"/> e lo stato della chiave
/// da <see cref="ISecretStore"/>, e li salva quando l'utente conferma. La chiave API
/// non viene mai ricaricata nella UI: mostriamo solo se è configurata o meno.
/// </para>
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly ISecretStore _secrets;
    private readonly ILlmProvider _llm;
    private readonly IStartupManager _startup;
    private readonly ILogger<SettingsViewModel> _logger;

    /// <summary>ViewModel del tab "Memoria" (albero delle memorie apprese).</summary>
    public MemoryViewModel Memory { get; }

    // --- Campi mostrati e modificabili nella UI (le proprietà sono generate da MVVM Toolkit). ---

    /// <summary>Nuova chiave API digitata dall'utente. Vuota = non modificare la chiave esistente.</summary>
    [ObservableProperty]
    private string _apiKey = string.Empty;

    /// <summary>True se una chiave API è già salvata nel secret store.</summary>
    [ObservableProperty]
    private bool _isApiKeyConfigured;

    /// <summary>Lingua dell'interfaccia selezionata ("it" o "en").</summary>
    [ObservableProperty]
    private string _uiLanguage = "it";

    [ObservableProperty]
    private string _geminiModel = string.Empty;

    [ObservableProperty]
    private double _screenCaptureFps;

    [ObservableProperty]
    private int _maxSessionMinutes;

    [ObservableProperty]
    private int _memoryMaintenanceIntervalHours;

    [ObservableProperty]
    private int _maxStoredSessions;

    [ObservableProperty]
    private int _obsoleteAfterDays;

    [ObservableProperty]
    private int _obsoleteMaxUses;

    [ObservableProperty]
    private int _maxActionSteps;

    [ObservableProperty]
    private int _memoryTreeDepth;

    [ObservableProperty]
    private bool _debugMode;

    [ObservableProperty]
    private bool _confirmRiskyActions;

    [ObservableProperty]
    private double _tokenPriceInputPerMillion;

    [ObservableProperty]
    private double _tokenPriceOutputPerMillion;

    [ObservableProperty]
    private string _costCurrencySymbol = "€";

    [ObservableProperty]
    private bool _checkForUpdatesOnStartup;

    /// <summary>Avvio automatico con Windows (stato letto/scritto dal registro, non da settings.json).</summary>
    [ObservableProperty]
    private bool _runAtStartup;

    /// <summary>Messaggio di esito mostrato all'utente dopo un'operazione.</summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>True se <see cref="StatusMessage"/> rappresenta un errore (per colorarlo di rosso).</summary>
    [ObservableProperty]
    private bool _statusIsError;

    public SettingsViewModel(
        ISettingsService settings,
        ISecretStore secrets,
        ILlmProvider llm,
        IStartupManager startup,
        MemoryViewModel memory,
        ILogger<SettingsViewModel> logger)
    {
        _settings = settings;
        _secrets = secrets;
        _llm = llm;
        _startup = startup;
        Memory = memory;
        _logger = logger;

        LoadFromSettings();
    }

    /// <summary>Copia i valori correnti delle impostazioni nei campi della UI.</summary>
    private void LoadFromSettings()
    {
        var s = _settings.Current;
        UiLanguage = s.UiLanguage;
        GeminiModel = s.GeminiModel;
        ScreenCaptureFps = s.ScreenCaptureFps;
        MaxSessionMinutes = s.MaxSessionMinutes;
        MemoryMaintenanceIntervalHours = s.MemoryMaintenanceIntervalHours;
        MaxStoredSessions = s.MaxStoredSessions;
        ObsoleteAfterDays = s.ObsoleteAfterDays;
        ObsoleteMaxUses = s.ObsoleteMaxUses;
        MaxActionSteps = s.MaxActionSteps;
        MemoryTreeDepth = s.MemoryTreeDepth;
        DebugMode = s.DebugMode;
        ConfirmRiskyActions = s.ConfirmRiskyActions;
        TokenPriceInputPerMillion = s.TokenPriceInputPerMillion;
        TokenPriceOutputPerMillion = s.TokenPriceOutputPerMillion;
        CostCurrencySymbol = s.CostCurrencySymbol;
        CheckForUpdatesOnStartup = s.CheckForUpdatesOnStartup;
        RunAtStartup = _startup.IsEnabled();

        IsApiKeyConfigured = _secrets.Has(SecretKeys.GeminiApiKey);
        ApiKey = string.Empty;
    }

    /// <summary>Salva impostazioni e (se inserita) la nuova chiave API, validandola con una chiamata leggera.</summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        bool keyChanged;
        try
        {
            // Partiamo da una copia delle impostazioni correnti e applichiamo i campi UI.
            var updated = _settings.Current.Clone();
            updated.UiLanguage = UiLanguage;
            updated.GeminiModel = GeminiModel;
            updated.ScreenCaptureFps = ScreenCaptureFps;
            updated.MaxSessionMinutes = MaxSessionMinutes;
            updated.MemoryMaintenanceIntervalHours = MemoryMaintenanceIntervalHours;
            updated.MaxStoredSessions = MaxStoredSessions;
            updated.ObsoleteAfterDays = ObsoleteAfterDays;
            updated.ObsoleteMaxUses = ObsoleteMaxUses;
            updated.MaxActionSteps = MaxActionSteps;
            updated.MemoryTreeDepth = MemoryTreeDepth;
            updated.DebugMode = DebugMode;
            updated.ConfirmRiskyActions = ConfirmRiskyActions;
            updated.TokenPriceInputPerMillion = TokenPriceInputPerMillion;
            updated.TokenPriceOutputPerMillion = TokenPriceOutputPerMillion;
            updated.CostCurrencySymbol = CostCurrencySymbol;
            updated.CheckForUpdatesOnStartup = CheckForUpdatesOnStartup;

            _settings.Save(updated);

            // L'avvio automatico è gestito a parte (registro di Windows), non nel file impostazioni.
            _startup.SetEnabled(RunAtStartup);

            // Applica subito la lingua scelta (UI in binding + cultura): l'overlay e le finestre
            // aperte si aggiornano senza riavviare l'app.
            LanguageManager.Apply(_settings.Current.UiLanguage);

            // La chiave si aggiorna solo se l'utente ne ha digitata una nuova.
            keyChanged = !string.IsNullOrWhiteSpace(ApiKey);
            if (keyChanged)
            {
                _secrets.Set(SecretKeys.GeminiApiKey, ApiKey.Trim());
                ApiKey = string.Empty;
            }

            // Rileggiamo i valori normalizzati (es. fps "limato" entro i limiti).
            LoadFromSettings();
            StatusIsError = false;
            StatusMessage = Loc.T("Settings_Saved");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Salvataggio impostazioni fallito.");
            StatusIsError = true;
            StatusMessage = Loc.T("Settings_SaveError", ex.Message);
            return;
        }

        // Validazione della chiave fuori dal blocco di salvataggio: il salvataggio è già riuscito,
        // qui diamo solo un riscontro (la chiave funziona oppure no) senza annullarlo.
        if (keyChanged)
            await ValidateSavedKeyAsync();
    }

    /// <summary>Verifica con Gemini che la chiave appena salvata sia valida, aggiornando lo stato.</summary>
    private async Task ValidateSavedKeyAsync()
    {
        StatusIsError = false;
        StatusMessage = Loc.T("Settings_VerifyingKey");
        try
        {
            await _llm.ValidateKeyAsync();
            StatusMessage = Loc.T("Settings_KeyValid");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Validazione della chiave API fallita.");
            StatusIsError = true;
            StatusMessage = Loc.T("Settings_KeyInvalid", ex.Message);
        }
    }

    /// <summary>Rimuove la chiave API salvata.</summary>
    [RelayCommand]
    private void RemoveApiKey()
    {
        try
        {
            _secrets.Delete(SecretKeys.GeminiApiKey);
            IsApiKeyConfigured = false;
            ApiKey = string.Empty;
            StatusIsError = false;
            StatusMessage = Loc.T("Settings_KeyRemoved");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rimozione chiave API fallita.");
            StatusIsError = true;
            StatusMessage = Loc.T("Settings_RemoveError", ex.Message);
        }
    }
}
