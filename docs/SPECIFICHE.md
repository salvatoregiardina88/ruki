# Specifiche – **Ruki**, agente AI per Windows

> Documento di specifiche funzionali e tecniche.
> Stato: bozza v2 · Data: 2026-06-19

---

## 1. Obiettivo

Creare un'applicazione desktop per Windows che funzioni da **agente AI personale e facilissimo da usare**. L'utente può:

1. **Insegnare** all'agente dei task (sessioni di addestramento registrate).
2. **Chiedere** all'agente di eseguire quei task, che l'agente svolge **pilotando il PC** (mouse/tastiera) mentre l'utente supervisiona.

Il valore centrale è la **memoria appresa**: ciò che l'utente insegna viene trasformato in conoscenza strutturata, organizzata e riutilizzabile.

---

## 2. Scelte tecnologiche

| Ambito | Scelta | Motivazione |
|---|---|---|
| Linguaggio | **C# / .NET 10** | SDK 10 già installato (current LTS); accesso diretto alle Win32 API necessarie (hook, input, cattura). |
| UI | **WPF** | Ideale per finestre overlay always-on-top, trasparenza, finestre senza bordo; più semplice di WinUI 3 per il deployment. |
| Storage | **SQLite** (`Microsoft.Data.Sqlite`) | File singolo, query relazionali per l'albero di memoria, zero server, backup banale. |
| Modello AI | **Google Gemini 3 (es. 3.1 Pro)** | Leader su **video + audio nativi** e ragionamento temporale: ingerisce il video reale con la traccia audio e allinea da solo parlato e immagini (vedi §10). Inoltre frontier più economico e contesto fino a **1M token**. |
| Astrazione modello | Interfaccia `ILlmProvider` | Un solo provider oggi (`GeminiProvider`); l'astrazione tiene il codice degli agenti disaccoppiato dal provider. |

### Perché Gemini per tutto
- L'esigenza chiave del Training è interpretare **audio e video simultaneamente** (es. l'utente dice "questa è la funzione" mentre la indica col mouse): i modelli **video-native** come Gemini sono addestrati esattamente per questa correlazione temporale, e ingeriscono il **video reale con audio** senza che dobbiamo ricostruire noi la timeline.
- Un solo provider = una sola chiave, un solo SDK, una sola fattura → meno complessità.
- Contesto da 1M token utile per sessioni lunghe e per navigare memorie grandi.

### Perché C# (e non altro)
- **Python**: ottimo per l'AI, ma overlay nativo, hook globali e input simulation su Windows sono più fragili e il packaging in `.exe` è più scomodo.
- **Electron/JS**: GUI comoda ma pesante e con accesso indiretto alle API di sistema.
- **C++**: massimo controllo ma sviluppo molto più lento, non necessario qui.

> **Niente OCR.** Non c'è alcun motore OCR separato nella pipeline: Gemini **legge nativamente** il testo dentro immagini e video come parte della comprensione visiva.

---

## 3. Architettura generale

```
┌──────────────────────────────────────────────────────────────┐
│                        UI (WPF)                               │
│  Overlay  ·  Chat  ·  Addestra  ·  Impostazioni               │
└───────────────┬──────────────────────────────────────────────┘
                │ comandi / eventi
┌───────────────▼──────────────────────────────────────────────┐
│                     Agent Orchestrator                        │
│   (chatta con l'utente, instrada verso gli altri agenti)      │
└───┬───────────────┬───────────────────┬──────────────────────┘
    │               │                   │
    ▼               ▼                   ▼
┌────────┐    ┌────────────┐      ┌────────────┐
│Training│    │  Memory    │      │  Action    │
│ Agent  │───▶│  Agent     │◀────▶│  Agent     │
└────────┘    └─────┬──────┘      └─────┬──────┘
                    │ legge/scrive       │ pilota PC
              ┌─────▼──────┐       ┌─────▼──────┐
              │  Memoria   │       │  Input/    │
              │  (SQLite,  │       │  Capture   │
              │   albero)  │       │  servizi   │
              └────────────┘       └────────────┘
```

**Servizi infrastrutturali trasversali:**
- `ILlmProvider` (chiamate a Gemini: testo, vision, video, function calling, embeddings)
- `IScreenCaptureService`, `IAudioCaptureService`, `IInputEventService` (hook), `IInputAutomationService` (SendInput)
- `IMemoryStore` (SQLite)
- `ISettingsService` / `ISecretStore` (Google API key cifrata con DPAPI)

---

## 4. Gli agenti

Ogni agente è una classe che costruisce un **contesto** (system prompt + dati) e chiama `ILlmProvider`. Sono coordinati dall'Orchestrator. Tutti possono **navigare la memoria** (vedi §8.3).

### 4.0 Gestione dello stato degli agenti

| Agente | Tipo | Stato |
|---|---|---|
| **Orchestrator** | **Conversazionale (stateful)** | Mantiene la **cronologia della conversazione** per tutta la sessione applicativa. **Azzerata alla chiusura/riapertura** (conversazioni effimere). |
| **Training** | Stateless / task-based | Una invocazione = una sessione di addestramento da processare. |
| **Memory** | Stateless / task-based | Una invocazione = una operazione di ingest o un job di manutenzione. |
| **Action** | Stateful **solo entro il task** | Mantiene lo stato del loop (storia screenshot/azioni) per la durata di un singolo task, poi si scarta. |

> Le informazioni durevoli (profilo utente, conoscenza appresa) **non** vivono nella cronologia chat ma nella **memoria** (§8): così sopravvivono al reset della conversazione.

### 4.1 Agent Orchestrator
- **Ruolo:** è l'agente con cui l'utente chatta. Fa da direttore d'orchestra.
- **Stato:** conversazionale (vedi §4.0).
- **Comportamento iniziale:** al primo avvio spiega come funziona l'app e chiede all'utente un'introduzione su di sé, sul lavoro e su come l'agente può essergli utile → salvata in memoria ("Profilo utente").
- **Responsabilità:** capire l'intento (chiacchierare / addestrare / **eseguire un task**); nell'ultimo caso recupera le memorie utili e invoca l'**Action Agent**. Richiama gli altri agenti via **function calling**.

### 4.2 Training Agent
- **Ruolo:** processa una sessione di addestramento e ne estrae conoscenza.
- **Input:** pacchetto sessione (§6) → **video con audio** (frame a basso fps + traccia audio) **+ screenshot su evento annotati col cursore** + testo chat con timeline + log eventi PC. Tutto passato a Gemini, che allinea nativamente parlato e immagini.
- **Prompt:** spiega cosa sono i dati, perché vengono inviati e l'output atteso (conoscenza strutturata, non riassunto generico).
- **Output atteso (JSON strutturato):** tutto ciò che serve per **riprodurre task simili**:
  - software/applicazioni usate;
  - processo e procedure passo-passo;
  - scorciatoie, percorsi UI, landmark visivi;
  - informazioni sui **dati mostrati** durante il training (dove si trovano, formato, significato);
  - pre/post-condizioni, casi particolari.
- L'output viene passato (con contesto) all'**Memory Agent**.

### 4.3 Memory Agent
- **Ruolo:** organizza e salva la conoscenza; mantiene la memoria pulita.
- **Input:** output del Training Agent + scheletro dell'albero attuale (titoli + riassunti).
- **Responsabilità:** decidere **dove** collocare la conoscenza (categoria esistente o nuova); scrivere/aggiornare i nodi foglia; **manutenzione periodica** (frequenza configurabile): riorganizzazione, pulizia, *pruning*, deduplicazione, fusione.
- **Output:** operazioni sul `IMemoryStore` (create/update/move/merge/delete) + log delle modifiche.

### 4.4 Action Agent
- **Ruolo:** esegue il task richiesto pilotando il PC.
- **Invocato da:** Orchestrator, che gli passa richiesta utente + contesto app + memorie rilevanti.
- **Modello di esecuzione (loop "computer use"):**
  1. Cattura screenshot corrente.
  2. Gemini (vision + **function calling**), guidato dalle memorie (la procedura appresa fa da "playbook"), decide la **prossima azione concreta** (click x,y / type / scroll / hotkey / wait), restituita come chiamata di funzione strutturata.
  3. L'azione viene eseguita via `IInputAutomationService` (movimenti **visibili**, così l'utente supervisiona).
  4. Nuovo screenshot → ripeti finché il task è completo o l'utente interviene.
- **Sicurezza:** l'utente può **mettere in pausa o stoppare in qualsiasi momento** (pulsante overlay + hotkey globale). Il loop controlla pausa/stop **prima di ogni azione**. Vedi §9.

---

## 5. Interfaccia utente

### 5.1 Overlay (default)
Piccola finestra always-on-top, senza bordo, trascinabile, con 4 pulsanti:
- **Chat** · **Addestra** · **Impostazioni** · **✕ (chiudi)**

Minimale e poco invasiva; ricompare con hotkey globale; durante l'esecuzione dell'Action Agent mostra **Pausa / Stop**.

### 5.2 Chat
- Area conversazione con l'Orchestrator: l'utente scrive e l'agente risponde.
- I messaggi durante una sessione di addestramento vengono **registrati con timestamp** nella timeline (§6).

### 5.3 Addestra
- Pulsante **Avvia/Stop sessione**.
- Timer visibile con durata massima configurabile (default **10 minuti**): al raggiungimento **non blocca**, ma mostra un **popup/notifica non bloccante** che invita a fermarsi.
- Indicatori "REC" per schermo/audio/eventi attivi.

### 5.4 Impostazioni
- **Tab "API"**: inserimento/validazione **Google API key** (salvata cifrata, §12), scelta modello, fps cattura, durata max sessione, frequenza manutenzione memoria, dispositivo audio.
- **Tab "Memoria"**: visualizzazione **ad albero** di ciò che l'agente ha imparato; **esplora, modifica, rinomina, sposta, elimina** i nodi e leggi/edita le memorie foglia.

---

## 6. Sottosistema di cattura (sessione di addestramento)

Tutto sincronizzato su una **timeline** (timestamp relativo all'inizio sessione, in ms).

| Sorgente | Cosa registra | Tecnologia |
|---|---|---|
| **Schermo (continuo)** | Frame a **pochi fps** (configurabile, default 1–2 fps) | GDI `BitBlt` / `Windows.Graphics.Capture` |
| **Schermo (su evento)** | Frame **extra al momento dell'evento** (click, doppio click, inizio/fine drag, cambio finestra, apertura menu), con **posizione del cursore evidenziata** sul frame | hook + cattura on-demand + annotazione |
| **Audio** | Microfono (+ opz. audio di sistema), salvato come WAV/MP3 e **multiplexato nel video** | NAudio (WASAPI/WaveIn, loopback per sistema) |
| **Chat** | Testo scritto dall'utente in chat con timestamp | UI |
| **Eventi PC** | Programma in primo piano (process name), titolo finestra, click (posizione, tasto), scroll, cambio finestra | hook globali `SetWindowsHookEx` (WH_MOUSE_LL), `SetWinEventHook` (foreground), `GetForegroundWindow`/`GetWindowText` |

**Frame su evento + cursore evidenziato** è la chiave per legare "cosa ha detto" a "cosa ha indicato/cliccato": migliora la comprensione del modello e in particolare la deissi ("*questa*" mentre punta).

- I dati di una sessione vengono salvati in una cartella dedicata (`/sessions/{id}/`) con: `video.mp4` (frame+audio), `events/` (screenshot su evento annotati), `chat.jsonl`, `events.jsonl`, `manifest.json`.
- Encoding del video con audio via l'**encoder integrato in Windows (Media Foundation / `MediaComposition`)**: parte core (il video viene passato a Gemini). License-safe, niente FFmpeg/GPL (vedi `docs/DIPENDENZE_E_LICENZE.md`).
- Scrittura **incrementale su disco durante** la registrazione, per non perdere dati in caso di crash.

---

## 7. Pipeline di training (allo Stop)

1. **Finalizzazione media**: mux di frame + audio in `video.mp4`; raccolta degli screenshot su evento (annotati) e della timeline eventi.
2. **Assemblaggio richiesta multimodale per Gemini**: video con audio + screenshot su evento (immagini ad alta definizione) + chat + timeline eventi. I file video/grandi vengono caricati con la **Files API** di Gemini e referenziati.
3. **Chiamata al Training Agent** con prompt strutturato → JSON di conoscenza appresa.
4. **Passaggio al Memory Agent** → salvataggio nell'albero.
5. Feedback in chat all'utente ("Ho imparato: …, l'ho salvato in …").

> **Nessuna trascrizione separata necessaria:** Gemini interpreta l'audio direttamente dal video, in sincrono con le immagini. (Se serve un testo della voce per la timeline/UI, lo si può chiedere allo stesso modello.)

---

## 8. Sottosistema memoria

### 8.1 Modello ad albero
- Struttura **a capitoli/sottocapitoli** con **livelli illimitati**.
- Nodi **interni** = categorie/sottocategorie (titolo + riassunto).
- Nodi **foglia** = memorie estese (contenuto completo).

### 8.2 Schema dati (SQLite)
```sql
CREATE TABLE memory_node (
  id           TEXT PRIMARY KEY,         -- GUID
  parent_id    TEXT REFERENCES memory_node(id),
  type         TEXT NOT NULL,            -- 'category' | 'memory'
  title        TEXT NOT NULL,
  summary      TEXT,                     -- breve, usato per la navigazione
  content      TEXT,                     -- esteso (solo foglie)
  metadata     TEXT,                     -- JSON: software, tag, fonte sessione...
  embedding    BLOB,                     -- opzionale (embeddings Gemini), per ricerca semantica futura
  created_at   INTEGER,
  updated_at   INTEGER,
  use_count    INTEGER DEFAULT 0,        -- per pruning/ranking
  last_used_at INTEGER
);
CREATE INDEX idx_parent ON memory_node(parent_id);
```

### 8.3 Navigazione (chiave del design)
Quando un agente serve memoria, **non** riceve tutte le memorie. Riceve lo **scheletro** dell'albero (titoli + riassunti dei figli del nodo corrente) ed espande i nodi **on-demand** tramite tool (function calling):
- `memory.getChildren(nodeId)` → figli con titolo+riassunto;
- `memory.getNode(nodeId)` → contenuto esteso della foglia;
- (futuro) `memory.search(query)` → ricerca semantica via embeddings Gemini.

Mantiene basso il consumo di token e scala a memorie molto grandi.

### 8.4 Manutenzione periodica
Job schedulato (frequenza configurabile) gestito dal Memory Agent:
- deduplicazione e fusione di memorie simili;
- *pruning* di nodi obsoleti/poco usati (`use_count`, `last_used_at`);
- riorganizzazione/ribilanciamento dell'albero;
- log delle modifiche, revisionabile nel tab Memoria.

---

## 9. Sottosistema azione (sicurezza)

- **Esecuzione visibile**: movimenti mouse/tastiera reali, così l'utente supervisiona.
- **Controllo utente sempre attivo**:
  - **Stop** = interrompe immediatamente; **Pausa** = sospende prima della prossima azione.
  - **Hotkey globale** sempre attiva.
  - Il loop verifica pausa/stop **prima di ogni singola azione**.
- **Modalità di conferma** (opzionale, configurabile): chiede conferma prima di azioni potenzialmente distruttive.
- **Guard-rail**: limiti su numero massimo di azioni/loop, timeout, blocco pattern pericolosi.

---

## 10. Integrazione modello AI (Gemini)

### 10.1 Astrazione
```csharp
interface ILlmProvider {
  Task<LlmResponse> CompleteAsync(LlmRequest req, CancellationToken ct);   // testo/vision/function calling
  Task<string> UploadFileAsync(string path, CancellationToken ct);         // Files API (video/immagini grandi)
  Task<float[]> EmbedAsync(string text, CancellationToken ct);             // embeddings (futuro)
}
```
Implementazione: `GeminiProvider`. L'astrazione tiene gli agenti indipendenti dal provider.

### 10.2 Modelli (Google Gemini)
- **Tutti gli agenti**: famiglia **Gemini 3** (es. **3.1 Pro**) per testo, vision, **video con audio**, function calling.
- **Embeddings** (memoria, futuro): modello di embedding Gemini.
- Il modello esatto è **configurabile** dalle impostazioni; verificare in fase di implementazione la versione flagship più aggiornata disponibile.

### 10.3 Gestione "video" e file
- Il **video con audio** viene caricato tramite **Files API** di Gemini e referenziato nella richiesta (no base64 per file grandi).
- Si possono passare nella stessa richiesta **video + immagini** (gli screenshot su evento ad alta definizione).
- **Niente OCR**: il testo a schermo è letto nativamente dal modello.

### 10.4 Gestione costi/token
- fps basso per il video; screenshot su evento solo nei momenti significativi.
- Riassunti (`summary`) per la navigazione memoria invece del contenuto completo.
- Caching del contesto dove supportato; tetto di spesa/avvisi configurabili (futuro).

---

## 11. Configurazioni (Impostazioni)

- Google API key (cifrata) · modello Gemini · (futuro) modello embeddings
- FPS cattura schermo (default 1–2)
- Durata massima sessione (default 10 min) → soglia avviso non bloccante
- Frequenza manutenzione memoria
- Dispositivo audio (mic / loopback sistema on-off)
- Hotkey: mostra overlay, stop/pausa azione
- Modalità conferma azioni (on/off)
- Cartella dati sessioni

---

## 12. Sicurezza e gestione API key

- Google API key salvata **cifrata a riposo** con **Windows DPAPI** (`ProtectedData`, scope `CurrentUser`) — mai in chiaro su file.
- I dati delle sessioni (video/audio/eventi) restano **locali** finché non inviati al modello per il training.
- Avvertenze UI prima di azioni che inviano dati al cloud.

> **Rimandati (non in questa fase):** mascheramento campi password in cattura; sostituzione futura con LLM locale privato. L'astrazione `ILlmProvider` lascia comunque aperta quest'ultima strada.

---

## 13. Requisiti non funzionali

- **Reattività UI**: cattura ed esecuzione su thread/servizi separati, UI mai bloccata.
- **Robustezza**: sessione di training salvata su disco *durante* la registrazione.
- **Estensibilità**: agenti e provider dietro interfacce; function calling per le capacità.
- **Osservabilità**: log strutturati (sessioni, chiamate LLM, azioni eseguite).
- **Installazione**: singolo `.exe` self-contained (o installer leggero).

---

## 14. Rischi e mitigazioni

| Rischio | Mitigazione |
|---|---|
| Costi token elevati (video/immagini) | fps basso, screenshot solo su evento, riassunti, tetto spesa |
| Affidabilità del "computer use" (click su coordinate sbagliate) | loop screenshot→azione con verifica, memorie come guida, modalità conferma |
| Hook globali bloccati da antivirus/permessi | documentare, fallback, eseguire come utente standard |
| Perdita dati sessione in crash | scrittura incrementale su disco |
| Upload video grandi lento | Files API resumable, fps basso, compressione |
| Modello AI non deterministico nell'azione | guard-rail, limiti, supervisione utente obbligatoria |

---

## 15. Domande aperte / decisioni future

1. **Audio di sistema** oltre al microfono nel training? (loopback WASAPI) — proposto opzionale.
2. **Ricerca semantica** (embeddings) sulla memoria: predisposta nello schema, da attivare in seconda battuta.
3. **Hotkey esatta** per stop/pausa azione e per richiamare l'overlay.
4. Versione esatta del modello Gemini da fissare in fase di implementazione.

### Decisioni già prese
- Nome: **Ruki** · Provider: **Gemini (tutto)** · Cattura: **fps + su evento con cursore evidenziato** · **No OCR**.
- Rimandati: mascheramento password, LLM locale.
