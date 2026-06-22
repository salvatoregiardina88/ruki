; ---------------------------------------------------------------------------------------------
;  Script Inno Setup per l'installer di Ruki.
;
;  Prerequisiti:
;    1) Eseguire prima  build/publish.ps1  -> produce publish/app/Ruki.App.exe (self-contained).
;    2) Installare Inno Setup (gratuito, https://jrsoftware.org/isinfo.php).
;
;  Creazione installer:
;    ISCC.exe build\ruki.iss      -> produce publish/installer/Ruki-Setup-<versione>.exe
;
;  Note:
;   - Installazione PER-UTENTE (niente diritti di amministratore): nessuna dipendenza di sistema,
;     l'eseguibile è self-contained.
;   - Il wizard è disponibile in italiano e inglese. La lingua dell'interfaccia di Ruki parte
;     comunque dalla lingua di sistema ed è poi cambiabile in Impostazioni.
; ---------------------------------------------------------------------------------------------

#define AppName "Ruki"
#define AppVersion "0.3.1"
#define AppPublisher "Ruki"
#define AppExeName "Ruki.App.exe"

[Setup]
; GUID stabile dell'applicazione (NON cambiarlo tra le versioni: serve a riconoscere gli aggiornamenti).
AppId={{8F3C1A92-5B6D-4E2A-9C7F-2D4B6E8A1C30}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
OutputDir=..\publish\installer
OutputBaseFilename=Ruki-Setup-{#AppVersion}
SetupIconFile=..\src\Ruki.App\Assets\Logo.ico
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Installazione per-utente: nessun prompt UAC, dati sotto il profilo dell'utente.
PrivilegesRequired=lowest

[Languages]
; "LicenseFile" mostra l'Informativa privacy con accettazione OBBLIGATORIA (una per lingua).
Name: "it"; MessagesFile: "compiler:Languages\Italian.isl"; LicenseFile: "..\src\Ruki.App\Assets\privacy_it.txt"
Name: "en"; MessagesFile: "compiler:Default.isl"; LicenseFile: "..\src\Ruki.App\Assets\privacy_en.txt"

[CustomMessages]
it.AutoStart=Avvia Ruki all'avvio di Windows
en.AutoStart=Launch Ruki at Windows startup

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; Flags: unchecked
; Avvio automatico con Windows: scrive la stessa voce di registro che l'app gestisce in Impostazioni.
Name: "startup"; Description: "{cm:AutoStart}"; Flags: unchecked

[Files]
; L'eseguibile self-contained (include .NET e tutte le dipendenze).
Source: "..\publish\app\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion
; Avvisi di licenza delle dipendenze di terze parti (incluse nell'eseguibile).
Source: "..\THIRD-PARTY-NOTICES.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
; Avvio automatico (per-utente). Stesso percorso/nome valore di RegistryStartupManager, così
; l'opzione resta coerente con la casella in Impostazioni. Rimosso alla disinstallazione.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; \
    ValueName: "Ruki"; ValueData: """{app}\{#AppExeName}"""; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
