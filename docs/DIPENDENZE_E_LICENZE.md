# Dipendenze e licenze – Ruki

> Policy e inventario delle dipendenze di terze parti.
> Stato: v1 · Data: 2026-06-19

---

## Policy

1. **Solo licenze permissive**: MIT, Apache-2.0, BSD, public domain.
   Compatibili con un prodotto **commerciale e closed-source**, **senza fee, royalty o copyleft**.
2. **Vietato**: GPL/LGPL e qualsiasi componente con royalty/brevetti a pagamento.
   In particolare **niente `ffmpeg`** e **niente encoder H.264/AAC di terze parti** (LGPL/GPL + royalty MPEG-LA).
3. **Codec video** (se mai serviranno): solo l'**encoder integrato in Windows** (Media Foundation),
   coperto dalla licenza del sistema operativo, oppure formati royalty-free (VP8/VP9/AV1, WebM).
   In alternativa si evita del tutto il video inviando a Gemini **fotogrammi (JPEG) + audio (WAV)**.
4. **Tutto incluso nel pacchetto**: l'utente installa **solo Ruki**. Pubblicazione
   *self-contained* (include anche il runtime .NET); nessuna dipendenza da installare a mano.
5. **Notices**: manteniamo l'elenco qui sotto; al packaging si genera un file di terze parti
   con i testi delle licenze.

> La licenza di **Ruki** stesso è una scelta libera del proprietario (anche modelli commerciali
> "paga una fee per uso commerciale"): nessuna delle dipendenze qui elencate la vincola.

---

## Inventario dipendenze

| Componente | Uso | Licenza |
|---|---|---|
| .NET 10 / runtime + WPF | piattaforma e UI | MIT |
| CommunityToolkit.Mvvm | MVVM (ViewModel, comandi) | MIT |
| Microsoft.Extensions.* (Hosting, DI, Logging, Options, Http) | composizione, DI, logging | MIT |
| Serilog (+ Extensions.Hosting, Sinks.File, Sinks.Debug) | logging su file | Apache-2.0 |
| Microsoft.Data.Sqlite | accesso al DB della memoria | MIT |
| SQLitePCLRaw.* (+ bundle_e_sqlite3) | provider nativo SQLite | Apache-2.0 |
| Motore SQLite | database | Public Domain |
| NAudio | cattura audio (microfono) | MIT |
| System.Drawing.Common | cattura schermo (GDI) e codifica JPEG | MIT |

Tutte permissive: **nessuna fee, nessun copyleft**.

---

## Note tecniche coerenti con la policy

- **Cattura schermo**: GDI/`System.Drawing` (MIT) → JPEG (formato libero, brevetti scaduti).
- **Audio**: NAudio (MIT) → WAV/PCM (formato libero).
- **Hook globali ed eventi**: P/Invoke Win32 (API del sistema operativo, nessuna libreria di terze parti).
- **Packaging**: `dotnet publish` **self-contained single-file** (win-x64): un solo `Ruki.App.exe` con runtime .NET incluso → l'utente non installa .NET. Script in `build/publish.ps1`. Installer opzionale via **Inno Setup** (licenza permissiva, uso commerciale consentito) con `build/ruki.iss`. Vedi `build/README.md`.
