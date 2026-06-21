# Piano di Implementazione – **Ruki** (agente AI per Windows)

> Companion di [SPECIFICHE.md](./SPECIFICHE.md)
> Stato: bozza v2 · Data: 2026-06-19

---

## 1. Struttura della soluzione (.NET 10)

Soluzione `Ruki.sln` con progetti separati per responsabilità:

```
Ruki.sln
├── Ruki.App          (WPF)         → UI: overlay, chat, addestra, impostazioni
├── Ruki.Core                       → domini, agenti, orchestrazione, interfacce
│     ├── Agents/      (Orchestrator, Training, Memory, Action)
│     ├── Memory/      (modello albero, navigazione)
│     ├── Llm/         (ILlmProvider, modelli request/response)
│     └── Abstractions/(IScreenCapture, IAudioCapture, IInputEvent, IInputAutomation, ISecretStore)
├── Ruki.Infrastructure             → implementazioni concrete
│     ├── Llm/Gemini/  (GeminiProvider: chat, vision, video, function calling, Files API)
│     ├── Capture/     (screen continuo + su evento, audio, hook eventi)
│     ├── Automation/  (SendInput)
│     ├── Storage/     (SqliteMemoryStore, SettingsService, DpapiSecretStore)
│     └── Sessions/    (registrazione/persistenza sessioni, encoding video)
└── Ruki.Tests                      → unit/integration test
```

DI con `Microsoft.Extensions.DependencyInjection` + `Hosting`.

---

## 2. Librerie (NuGet) candidate

| Scopo | Pacchetto |
|---|---|
| DI / Hosting / Config / Logging | `Microsoft.Extensions.*` |
| SQLite | `Microsoft.Data.Sqlite` (+ Dapper opz.) |
| **Gemini** | SDK ufficiale **Google Gen AI per .NET** (`Google.GenAI`); fallback `HttpClient` verso l'API Gemini + **Files API** per i video |
| Audio | `NAudio` |
| Encoding video (mux frame+audio) | **Windows Media Foundation** via WinRT `MediaComposition` (encoder del SO, license-safe — niente FFmpeg) |
| Hook globali mouse/tastiera/finestra | `SharpHook` (o P/Invoke `SetWindowsHookEx`/`SetWinEventHook`) |
| Input automation (SendInput) | `H.InputSimulator` (o P/Invoke `SendInput`) |
| Cattura schermo | `Windows.Graphics.Capture` via CsWinRT, **oppure** GDI `BitBlt` (`System.Drawing.Common`) per semplicità |
| Cifratura API key | `System.Security.Cryptography.ProtectedData` (DPAPI) |
| JSON | `System.Text.Json` |

> Per ogni libreria nativa pesante esiste un fallback P/Invoke: preferire la via più semplice prima, ottimizzare poi.

---

## 3. Fasi (milestone) e criteri di accettazione

Ordine pensato per avere **valore dimostrabile presto** e isolare le parti difficili (cattura/azione).

### Fase 0 — Scaffolding & fondamenta
- Soluzione, progetti, DI, logging, config.
- `ISecretStore` con DPAPI; `ISettingsService` (lettura/scrittura impostazioni).
- ✅ *Accettazione:* l'app parte; salva/legge un'impostazione e la **Google API key** cifrata.

### Fase 1 — Shell UI overlay
- Overlay always-on-top, senza bordo, trascinabile, 4 pulsanti (Chat/Addestra/Impostazioni/✕).
- Navigazione tra le viste; finestra Impostazioni con tab "API" e "Memoria" (placeholder).
- ✅ *Accettazione:* overlay funzionante, tab navigabili, API key inseribile e persistita.

### Fase 2 — Gemini provider + Orchestrator (chat)
- `ILlmProvider` + `GeminiProvider` (generateContent: testo + function calling).
- Orchestrator **conversazionale** (cronologia in memoria, reset a chiusura).
- Vista Chat funzionante; messaggio di benvenuto + richiesta introduzione utente.
- ✅ *Accettazione:* l'utente chatta con l'agente; risposte reali da Gemini; cronologia mantenuta nella sessione.

### Fase 3 — Memoria (albero) + tab Memoria
- `IMemoryStore` su SQLite (schema §8.2), CRUD nodi.
- API di **navigazione** (`getChildren`, `getNode`) esposte come **tool/function** agli agenti.
- Tab "Memoria": visualizza/modifica/sposta/elimina nodi.
- Orchestrator salva il **profilo utente** in memoria.
- ✅ *Accettazione:* creo categorie/memorie, le navigo nell'albero; l'agente le legge via function calling.

### Fase 4 — Cattura sessione di addestramento
- `IScreenCaptureService` (frame a fps + **frame su evento con cursore evidenziato**), `IAudioCaptureService` (NAudio), `IInputEventService` (hook eventi PC), cattura testo chat con timeline.
- Persistenza incrementale su disco (`/sessions/{id}/`) + encoding `video.mp4` (frame+audio) con Media Foundation.
- UI "Addestra": avvia/stop, timer, **popup non bloccante** alla soglia (default 10 min).
- ✅ *Accettazione:* una sessione produce video+audio sincronizzati, screenshot su evento annotati, chat ed eventi su timeline, salvati su disco; avviso alla scadenza.

### Fase 5 — Pipeline Training → Memory
- Upload video via **Files API** Gemini; assemblaggio richiesta multimodale (video+audio + screenshot evento + chat + eventi).
- **Training Agent** → JSON conoscenza appresa (prompt strutturato).
- **Memory Agent** → colloca/salva nell'albero; feedback in chat.
- ✅ *Accettazione:* a fine sessione l'agente "impara" e salva nodi memoria coerenti; l'utente vede cosa ha imparato.

### Fase 6 — Manutenzione memoria
- Job schedulato (frequenza configurabile): dedup, pruning, fusione, riorganizzazione.
- Log modifiche revisionabile nel tab Memoria.
- ✅ *Accettazione:* eseguendo il job, memorie duplicate vengono fuse e nodi obsoleti rimossi, con log.

### Fase 7 — Action Agent (esecuzione)
- `IInputAutomationService` (SendInput: move/click/scroll/type/hotkey).
- Loop "computer use": screenshot → decisione Gemini (vision + function calling, guidata dalle memorie) → azione → ripeti.
- **Controlli sicurezza**: Pausa/Stop (UI + hotkey globale), check prima di ogni azione, limiti/guard-rail, modalità conferma opzionale.
- ✅ *Accettazione:* dato un task appreso, l'agente lo esegue pilotando il PC; l'utente può fermarlo/metterlo in pausa in ogni momento.

### Fase 8 — Rifinitura & packaging
- ✅ **Resilienza di rete:** retry con backoff esponenziale sugli errori transitori di Gemini (rete/timeout, 429, 5xx); errori di rete definitivi tradotti in messaggi chiari.
- ✅ **Validazione chiave API:** al salvataggio di una nuova chiave, verifica leggera con Gemini (esito mostrato in Impostazioni).
- ✅ **Single-instance:** mutex all'avvio (niente due overlay con hook globali in conflitto).
- ✅ **Chiusura pulita:** se l'app si chiude durante una registrazione, `ITrainingSessionRecorder.Abort()` rilascia hook/audio/file senza bloccare lo shutdown sulla codifica.
- ✅ **Build self-contained (`.exe`) / installer:** `build/publish.ps1` (single-file win-x64, niente .NET da installare) + `build/ruki.iss` (Inno Setup, wizard IT/EN, per-utente). Vedi `build/README.md`.
- **Lingua all'installazione:** la UI è già localizzata IT/EN (`UiLanguage`, default = lingua di sistema al primo avvio); il wizard dell'installer è IT/EN. Pre-impostare `UiLanguage` dall'installer resta un'opzione futura (oggi basta il default di sistema).
- ✅ *Accettazione:* build distribuibile (eseguibile self-contained verificato); flusso end-to-end (insegna → chiedi → esegui) funzionante.

> Rimandati esplicitamente (non in queste fasi): mascheramento campi password in cattura; sostituzione con LLM locale.

---

## 4. MVP consigliato

**MVP = Fasi 0–5 + 7** (anche senza manutenzione avanzata, Fase 6).
A quel punto l'app: chatta, impara da una sessione registrata (video+audio nativi a Gemini), salva in memoria ad albero, ed **esegue** un task appreso. Fase 6 e rifiniture seguono.

Variante "demo rapida" per validare presto il rischio AI: Fase 0 → 2 → 3 → 5 (con cattura minima) per vedere subito il ciclo apprendi/ricorda.

---

## 5. Decisioni tecniche da fissare in Fase 4/7

1. Cattura schermo: GDI `BitBlt` (semplice) vs `Windows.Graphics.Capture` (moderno, DPI/HDR) — **proposta:** iniziare con GDI.
2. Hook & input: `SharpHook`/`H.InputSimulator` vs P/Invoke puro — **proposta:** libreria prima.
3. Audio di sistema (loopback) oltre al mic — opzionale, dietro impostazione.
4. Versione esatta del modello Gemini.
5. Hotkey globale stop/pausa e richiamo overlay.

---

## 6. Strategia di test

- **Unit**: memoria (CRUD/navigazione albero), parsing output agenti, settings/secret store, annotazione cursore su frame.
- **Integration**: `GeminiProvider` (con chiave reale, dietro flag), pipeline training su una sessione registrata d'esempio.
- **Manuale/E2E**: cattura, esecuzione azione (richiede supervisione umana per natura).
- Mock di `ILlmProvider` per testare gli agenti senza costi.

---

## 7. Prossimo passo proposto

Partire da **Fase 0 + 1**: scaffolding della soluzione e overlay UI funzionante con Impostazioni/API key (Google). Poi **Fase 2** per avere subito la chat reale con Gemini.

> Serve solo la **Google API key** (Google AI Studio, c'è un free tier) per far girare dalla Fase 2 in poi.
