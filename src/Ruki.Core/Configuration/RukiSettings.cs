using System.Globalization;

namespace Ruki.Core.Configuration;

/// <summary>
/// Impostazioni applicative di Ruki, persistite su file (vedi ISettingsService).
/// <para>
/// È un semplice contenitore di dati (POCO): nessuna logica oltre alla normalizzazione
/// dei valori. Viene serializzato/deserializzato in JSON, quindi tutte le proprietà
/// devono avere un getter/setter pubblico e un valore di default sensato.
/// </para>
/// <para>
/// NOTA: i segreti (es. la API key di Gemini) NON stanno qui — vengono salvati cifrati
/// tramite <c>ISecretStore</c>. Questo file resta in chiaro e leggibile dall'utente.
/// </para>
/// </summary>
public sealed class RukiSettings
{
    /// <summary>
    /// Versione dello schema delle impostazioni. Serve in futuro per migrare un file
    /// salvato da una versione precedente dell'app senza perderne il contenuto.
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// Identificativo del modello Gemini da usare per tutti gli agenti.
    /// Configurabile perché i nomi dei modelli cambiano nel tempo: va verificato
    /// il modello flagship disponibile sull'account Google.
    /// </summary>
    public string GeminiModel { get; set; } = "gemini-3.5-flash";

    /// <summary>
    /// Lingua dell'interfaccia: "it" o "en". Al primo avvio parte dalla lingua di sistema, così
    /// l'installazione non richiede scelte; l'utente può poi cambiarla dalle impostazioni.
    /// </summary>
    public string UiLanguage { get; set; } = DefaultUiLanguage();

    /// <summary>
    /// Frame al secondo per la cattura "continua" dello schermo durante l'addestramento.
    /// Volutamente basso (stiamo registrando screenshot, non un video fluido).
    /// </summary>
    public double ScreenCaptureFps { get; set; } = 1.5;

    /// <summary>
    /// Durata massima consigliata di una sessione di addestramento, in minuti.
    /// Al raggiungimento la sessione NON si ferma da sola: mostra un avviso non bloccante.
    /// </summary>
    public int MaxSessionMinutes { get; set; } = 10;

    /// <summary>
    /// Ogni quante ore l'agente della memoria esegue la manutenzione
    /// (deduplica, pruning, riorganizzazione).
    /// </summary>
    public int MemoryMaintenanceIntervalHours { get; set; } = 8;

    /// <summary>
    /// Quante sessioni di addestramento registrate conservare su disco: le più vecchie oltre
    /// questo numero vengono eliminate automaticamente per non riempire il disco.
    /// </summary>
    public int MaxStoredSessions { get; set; } = 10;

    /// <summary>
    /// Durante la manutenzione, una memoria viene marcata come "obsoleta" (non eliminata) se non è
    /// usata da almeno questi giorni E ha meno di <see cref="ObsoleteMaxUses"/> utilizzi.
    /// </summary>
    public int ObsoleteAfterDays { get; set; } = 90;

    /// <summary>Soglia di utilizzi sotto la quale una memoria vecchia può diventare obsoleta.</summary>
    public int ObsoleteMaxUses { get; set; } = 2;

    /// <summary>
    /// Se true, l'agente dell'azione chiede conferma prima di eseguire operazioni
    /// potenzialmente distruttive.
    /// </summary>
    public bool ConfirmRiskyActions { get; set; } = true;

    /// <summary>
    /// Numero massimo di passi (azioni) che l'agente dell'azione può eseguire per un singolo
    /// compito, come guard-rail contro loop o comportamenti fuori controllo.
    /// </summary>
    public int MaxActionSteps { get; set; } = 200;

    /// <summary>
    /// Modalità debug: se attiva, durante l'esecuzione di un'azione mostra una finestra con la
    /// conversazione completa con l'Action Agent (testo, screenshot e risposte).
    /// </summary>
    public bool DebugMode { get; set; }

    /// <summary>
    /// Profondità dell'albero di memoria inviato inizialmente all'agente (solo titoli/riassunti):
    /// oltre questo livello l'agente deve espandere i nodi su richiesta.
    /// </summary>
    public int MemoryTreeDepth { get; set; } = 2;

    /// <summary>
    /// Se true, all'avvio Ruki verifica in modo silenzioso la presenza di un aggiornamento e,
    /// se disponibile, chiede all'utente se aggiornare.
    /// </summary>
    public bool CheckForUpdatesOnStartup { get; set; } = true;

    /// <summary>
    /// Prezzo (per 1 milione di token) usato per STIMARE il costo, lato input (token inviati).
    /// Valore indicativo del modello di default: va verificato sul listino Gemini corrente.
    /// </summary>
    public double TokenPriceInputPerMillion { get; set; } = 4.0;

    /// <summary>Prezzo (per 1 milione di token) lato output (token ricevuti). Vedi sopra.</summary>
    public double TokenPriceOutputPerMillion { get; set; } = 18.0;

    /// <summary>Simbolo di valuta usato nella stima del costo.</summary>
    public string CostCurrencySymbol { get; set; } = "€";

    /// <summary>
    /// Istante (UTC) dell'ultima manutenzione della memoria andata a buon fine. <c>null</c> se non
    /// è mai stata eseguita. Lo mostriamo nel tab Memoria; non è un parametro configurabile.
    /// </summary>
    public DateTimeOffset? LastMemoryMaintenanceUtc { get; set; }

    /// <summary>
    /// Riporta i valori entro intervalli validi, proteggendo l'app da un file di
    /// impostazioni modificato a mano con valori assurdi (fps negativi, durate enormi…).
    /// Restituisce la stessa istanza per comodità di concatenazione.
    /// </summary>
    public RukiSettings Normalize()
    {
        ScreenCaptureFps = Math.Clamp(ScreenCaptureFps, 0.2, 10.0);
        MaxSessionMinutes = Math.Clamp(MaxSessionMinutes, 1, 60);
        MemoryMaintenanceIntervalHours = Math.Clamp(MemoryMaintenanceIntervalHours, 1, 24 * 30);
        MaxStoredSessions = Math.Clamp(MaxStoredSessions, 1, 1000);
        ObsoleteAfterDays = Math.Clamp(ObsoleteAfterDays, 1, 3650);
        ObsoleteMaxUses = Math.Clamp(ObsoleteMaxUses, 0, 1000);
        MaxActionSteps = Math.Clamp(MaxActionSteps, 1, 200);
        MemoryTreeDepth = Math.Clamp(MemoryTreeDepth, 1, 10);

        TokenPriceInputPerMillion = Math.Max(0, TokenPriceInputPerMillion);
        TokenPriceOutputPerMillion = Math.Max(0, TokenPriceOutputPerMillion);
        if (string.IsNullOrWhiteSpace(CostCurrencySymbol))
            CostCurrencySymbol = "€";

        if (string.IsNullOrWhiteSpace(GeminiModel))
            GeminiModel = "gemini-3.5-flash";

        // Solo le due lingue supportate; qualsiasi altro valore torna alla lingua di sistema.
        UiLanguage = UiLanguage is "it" or "en" ? UiLanguage : DefaultUiLanguage();

        return this;
    }

    /// <summary>Lingua predefinita ricavata dalla cultura di sistema: italiano se il sistema è in italiano, altrimenti inglese.</summary>
    private static string DefaultUiLanguage()
        => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("it", StringComparison.OrdinalIgnoreCase)
            ? "it"
            : "en";

    /// <summary>
    /// Copia superficiale delle impostazioni. Le proprietà sono tutte tipi valore o
    /// stringhe (immutabili), quindi una copia membro-a-membro è sufficiente e sicura.
    /// Serve per poter modificare le impostazioni nella UI senza toccare l'istanza
    /// "viva" finché l'utente non salva.
    /// </summary>
    public RukiSettings Clone() => (RukiSettings)MemberwiseClone();
}
