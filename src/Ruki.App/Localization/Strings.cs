namespace Ruki.App.Localization;

/// <summary>
/// Tabella delle stringhe dell'interfaccia nelle due lingue supportate (italiano e inglese).
/// <para>
/// Scelta volutamente minimale: invece di file di risorse (.resx), che con la sola build da riga
/// di comando richiederebbero generatori dedicati, le traduzioni stanno qui in un dizionario in
/// chiaro. Con due sole lingue è la soluzione più semplice da leggere e mantenere.
/// </para>
/// <para>
/// Le chiavi sono raggruppate per area della UI. Nei valori, <c>{0}</c>, <c>{1}</c>… sono
/// segnaposto di <see cref="string.Format(string, object?[])"/> per i messaggi con dati variabili.
/// </para>
/// </summary>
internal static class Strings
{
    // Chiave → (italiano, inglese).
    private static readonly Dictionary<string, (string It, string En)> Table = new(StringComparer.Ordinal)
    {
        // --- Comuni ---
        ["Common_Save"] = ("Salva", "Save"),
        ["Common_Stop"] = ("Stop", "Stop"),
        ["Common_Close"] = ("Chiudi", "Close"),

        // --- Titoli finestre ---
        ["Title_Chat"] = ("Chat - Ruki", "Chat - Ruki"),
        ["Title_Settings"] = ("Impostazioni - Ruki", "Settings - Ruki"),
        ["Title_Privacy"] = ("Informativa privacy - Ruki", "Privacy policy - Ruki"),
        ["Title_ActionDebug"] = ("Debug azione - Ruki", "Action debug - Ruki"),
        ["Title_Image"] = ("Immagine - Ruki", "Image - Ruki"),

        // --- Overlay ---
        ["Overlay_Chat"] = ("Chat", "Chat"),
        ["Overlay_Teach"] = ("Insegna", "Teach"),
        ["Overlay_Settings"] = ("Impostazioni", "Settings"),
        ["Overlay_CloseTip"] = ("Chiudi Ruki", "Close Ruki"),
        ["Overlay_MicTip"] = ("Microfono: clic per mutare/riattivare", "Microphone: click to mute/unmute"),
        ["Overlay_DiscardTip"] = ("Annulla insegnamento (elimina la registrazione)", "Discard teaching (delete the recording)"),
        ["Overlay_Pause"] = ("Pausa", "Pause"),
        ["Overlay_Resume"] = ("Riprendi", "Resume"),

        // --- Apprendimento (stato mostrato nell'overlay dopo lo stop) ---
        ["Learn_Reorganizing"] = ("Sto riorganizzando le idee prima di apprendere…",
                                  "Organizing my thoughts before learning…"),
        ["Learn_SessionCloseError"] = ("Errore nella chiusura della sessione (vedi log).",
                                       "Error while closing the session (see log)."),
        ["Learn_Learning"] = ("Sto imparando da questa sessione…", "Learning from this session…"),
        ["Learn_LearnedOne"] = ("Imparato! 1 memoria creata.", "Learned! 1 memory created."),
        ["Learn_LearnedMany"] = ("Imparato! {0} memorie create.", "Learned! {0} memories created."),
        ["Learn_Failed"] = ("Apprendimento non riuscito (vedi log).", "Learning failed (see log)."),
        ["Learn_Discarded"] = ("Insegnamento annullato.", "Teaching discarded."),

        // --- Chat ---
        ["Chat_Typing"] = ("Ruki sta scrivendo…", "Ruki is typing…"),
        ["Chat_Send"] = ("Invia", "Send"),
        ["Chat_Executing"] = ("▶ Eseguo: {0}", "▶ Running: {0}"),
        ["Chat_UnexpectedError"] = ("Errore imprevisto: {0}", "Unexpected error: {0}"),

        // --- Esecuzione compito (Action Agent) ---
        ["Action_Running"] = ("Sto eseguendo: {0}  (premi Esc per fermare)",
                              "Running: {0}  (press Esc to stop)"),
        ["Action_Resumed"] = ("Ripreso.", "Resumed."),
        ["Action_Paused"] = ("In pausa.", "Paused."),
        ["Action_Done"] = ("Fatto: {0}", "Done: {0}"),
        ["Action_NotDone"] = ("Non riuscito: {0}", "Failed: {0}"),
        ["Action_DoneNoDetail"] = ("Fatto.", "Done."),
        ["Action_FailedNoDetail"] = ("Non riuscito.", "Failed."),
        ["Action_MaxSteps"] = ("Fermato: raggiunto il limite di {0} passi.", "Stopped: reached the limit of {0} steps."),
        ["Action_Interrupted"] = ("Esecuzione interrotta.", "Execution interrupted."),
        ["Action_Error"] = ("Errore: {0}", "Error: {0}"),
        // Versioni brevi per l'overlay (una sola riga di stato): il dettaglio completo va in chat.
        ["Action_DoneShort"] = ("Fatto", "Done"),
        ["Action_FailedShort"] = ("Non riuscito", "Failed"),
        ["Action_InterruptedShort"] = ("Interrotto", "Interrupted"),
        ["Action_ErrorShort"] = ("Errore", "Error"),
        ["Update_Title"] = ("Aggiornamento disponibile", "Update available"),
        ["Update_Body"] = ("È disponibile la versione {0} di Ruki. Vuoi aprire la pagina per scaricarla?",
                           "Ruki version {0} is available. Open the download page?"),
        ["Confirm_Title"] = ("Ruki — conferma azione", "Ruki — confirm action"),
        ["Confirm_Body"] = ("Ruki sta per eseguire un'azione potenzialmente rischiosa:\n\n{0}\n\nProcedere?",
                            "Ruki is about to perform a potentially risky action:\n\n{0}\n\nProceed?"),
        ["ActionDebug_ZoomTip"] = ("Clicca per ingrandire", "Click to enlarge"),
        ["Debug_SystemInstructions"] = ("Istruzioni di sistema", "System instructions"),
        ["Debug_AgentReply"] = ("Risposta dell'agente", "Agent reply"),
        ["Debug_SentToModel"] = ("Inviato al modello", "Sent to the model"),
        ["Debug_Step"] = ("Passo {0} · {1}", "Step {0} · {1}"),

        // --- Impostazioni: etichette ---
        ["Settings_TabApi"] = ("API", "API"),
        ["Settings_TabAdvanced"] = ("Avanzate", "Advanced"),
        ["Settings_TabMemory"] = ("Memoria", "Memory"),
        ["Settings_SecModelLang"] = ("Modello e lingua", "Model & language"),
        ["Settings_SecRecording"] = ("Registrazione", "Recording"),
        ["Settings_SecMemory"] = ("Memoria", "Memory"),
        ["Settings_SecActions"] = ("Esecuzione azioni", "Action execution"),
        ["Settings_SecCosts"] = ("Stima costi", "Cost estimate"),
        ["Settings_PriceInput"] = ("Prezzo input (per 1M token)", "Input price (per 1M tokens)"),
        ["Settings_PriceOutput"] = ("Prezzo output (per 1M token)", "Output price (per 1M tokens)"),
        ["Settings_Currency"] = ("Valuta", "Currency"),
        ["Settings_SecStartup"] = ("Avvio", "Startup"),
        ["Settings_RunAtStartup"] = ("Avvia Ruki all'avvio di Windows", "Launch Ruki at Windows startup"),
        ["Settings_CheckUpdates"] = ("Verifica aggiornamenti all'avvio", "Check for updates at startup"),
        ["Settings_ApiKey"] = ("Chiave API Google Gemini", "Google Gemini API key"),
        ["Settings_ApiKeyHint"] = ("La chiave viene salvata cifrata sul tuo profilo Windows. Lascia vuoto per non modificarla.",
                                   "The key is stored encrypted in your Windows profile. Leave empty to keep it unchanged."),
        ["Settings_ApiKeyHelp"] = ("Non sai come ottenerne una? Clicca qui", "Don't know how to get one? Click here"),
        ["Settings_Privacy"] = ("Informativa sulla privacy", "Privacy policy"),
        ["Settings_Status"] = ("Stato:", "Status:"),
        ["Settings_NoKey"] = ("nessuna chiave configurata", "no key configured"),
        ["Settings_KeyConfigured"] = ("chiave configurata", "key configured"),
        ["Settings_RemoveKey"] = ("Rimuovi chiave", "Remove key"),
        ["Settings_Model"] = ("Modello Gemini", "Gemini model"),
        ["Settings_Fps"] = ("FPS cattura schermo (insegnamento)", "Screen capture FPS (teaching)"),
        ["Settings_MaxSession"] = ("Durata massima sessione (minuti)", "Max session length (minutes)"),
        ["Settings_MaintInterval"] = ("Manutenzione memoria ogni (ore)", "Memory maintenance every (hours)"),
        ["Settings_MaxSessions"] = ("Sessioni di insegnamento da conservare", "Teaching sessions to keep"),
        ["Settings_ObsoleteDays"] = ("Archivia memorie non usate da (giorni)", "Archive memories unused for (days)"),
        ["Settings_ObsoleteUses"] = ("…e con meno di (utilizzi)", "…and with fewer than (uses)"),
        ["Settings_MaxSteps"] = ("Massimo numero di passi per azione", "Max steps per action"),
        ["Settings_TreeDepth"] = ("Profondità albero memoria inviato all'agente", "Memory tree depth sent to the agent"),
        ["Settings_ConfirmRisky"] = ("Chiedi conferma prima di azioni rischiose", "Ask for confirmation before risky actions"),
        ["Settings_DebugMode"] = ("Modalità debug (mostra la finestra dell'agente azione)",
                                  "Debug mode (show the action agent window)"),
        ["Settings_Language"] = ("Lingua interfaccia", "Interface language"),

        // --- Impostazioni: messaggi di esito ---
        ["Settings_Saved"] = ("Impostazioni salvate.", "Settings saved."),
        ["Settings_SaveError"] = ("Errore nel salvataggio: {0}", "Error while saving: {0}"),
        ["Settings_VerifyingKey"] = ("Salvato. Verifico la chiave API…", "Saved. Verifying the API key…"),
        ["Settings_KeyValid"] = ("Salvato. Chiave API valida.", "Saved. API key is valid."),
        ["Settings_KeyInvalid"] = ("Salvato, ma la chiave API non è valida: {0}", "Saved, but the API key is not valid: {0}"),
        ["Settings_KeyRemoved"] = ("Chiave API rimossa.", "API key removed."),
        ["Settings_RemoveError"] = ("Errore nella rimozione: {0}", "Error while removing: {0}"),

        // --- Memoria: comandi e editor ---
        ["Memory_Refresh"] = ("Aggiorna", "Refresh"),
        ["Memory_AddCategory"] = ("+ Categoria", "+ Category"),
        ["Memory_AddMemory"] = ("+ Memoria", "+ Memory"),
        ["Memory_Delete"] = ("Elimina", "Delete"),
        ["Memory_Archive"] = ("Archivia", "Archive"),
        ["Memory_Reactivate"] = ("Riattiva", "Reactivate"),
        ["Memory_ToggleTip"] = ("Archivia o riattiva la memoria selezionata",
                                "Archive or reactivate the selected memory"),
        ["Memory_Maintenance"] = ("Manutenzione", "Maintenance"),
        ["Memory_LastMaintenance"] = ("Ultima manutenzione: {0}", "Last maintenance: {0}"),
        ["Memory_NeverRun"] = ("mai", "never"),
        ["Memory_MaintenanceTip"] = ("Unisce i duplicati, riorganizza le categorie e archivia le memorie inutilizzate",
                                     "Merges duplicates, reorganizes categories and archives unused memories"),
        ["Memory_Title"] = ("Titolo", "Title"),
        ["Memory_Summary"] = ("Riassunto", "Summary"),
        ["Memory_Content"] = ("Contenuto", "Content"),

        // --- Memoria: messaggi di esito ---
        ["Memory_MaintRunning"] = ("Manutenzione in corso… (può richiedere qualche secondo)",
                                   "Maintenance running… (it may take a few seconds)"),
        ["Memory_Error"] = ("Errore: {0}", "Error: {0}"),
        ["Memory_NewCategory"] = ("Nuova categoria", "New category"),
        ["Memory_NewMemory"] = ("Nuova memoria", "New memory"),
        ["Memory_Saved"] = ("Salvato.", "Saved."),
        ["Memory_Deleted"] = ("Eliminato.", "Deleted."),
        ["Memory_Archived"] = ("Memoria archiviata.", "Memory archived."),
        ["Memory_Reactivated"] = ("Memoria riattivata.", "Memory reactivated."),
        ["Memory_CategoryAdded"] = ("Categoria aggiunta.", "Category added."),
        ["Memory_MemoryAdded"] = ("Memoria aggiunta.", "Memory added."),
    };

    /// <summary>Restituisce la traduzione della chiave nella lingua data ("en" o, per default, "it").</summary>
    public static string Get(string language, string key)
        => Table.TryGetValue(key, out var v)
            ? (string.Equals(language, "en", StringComparison.Ordinal) ? v.En : v.It)
            : key;   // chiave mancante: mostriamo la chiave stessa, così l'errore è evidente
}
