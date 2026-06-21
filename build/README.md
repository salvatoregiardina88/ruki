# Build & packaging di Ruki

Ruki viene distribuito come **eseguibile self-contained**: un singolo `.exe` che include il runtime
.NET e tutte le dipendenze, così l'utente finale **installa solo Ruki** (nessun .NET da installare a
parte), come richiesto dai vincoli del progetto.

## 1. Pubblicare l'eseguibile

```pwsh
pwsh build/publish.ps1
```

Produce `publish/app/Ruki.App.exe` (~80 MB, compresso). Già eseguibile così com'è.

## 2. Creare l'installer (facoltativo)

Serve [Inno Setup](https://jrsoftware.org/isinfo.php) (gratuito, licenza permissiva, uso commerciale
consentito → compatibile con i vincoli di licenza di Ruki).

```pwsh
ISCC.exe build\ruki.iss
```

Produce `publish/installer/Ruki-Setup-<versione>.exe`. L'installer:

- è **per-utente** (nessun prompt UAC, dati sotto il profilo dell'utente);
- ha il wizard in **italiano e inglese**;
- installa il solo `Ruki.App.exe`;
- offre opzioni (non selezionate di default): **icona sul desktop** e **avvio automatico con Windows**
  (quest'ultima scrive la stessa voce di registro gestita dall'app in Impostazioni → Avanzate → Avvio);
- mostra l'**Informativa privacy** con **accettazione obbligatoria** (una per lingua).

L'informativa privacy (IT/EN) è in `src/Ruki.App/Assets/privacy_it.txt` e `privacy_en.txt`: è il testo
**canonico** usato sia dall'installer (licenza da accettare) sia dall'app (link in Impostazioni → API).
Va salvato in **UTF-8 con BOM**. Per aggiornarla, modificare quei file.

La lingua dell'interfaccia di Ruki parte dalla lingua di sistema ed è poi cambiabile in
**Impostazioni → API → Lingua interfaccia**.

## Note

- I dati dell'app (impostazioni, memoria, sessioni, log) restano sotto `%APPDATA%\Ruki` e non
  vengono toccati dalla disinstallazione.
- La cartella `publish/` è un artefatto di build rigenerabile: può essere eliminata.
