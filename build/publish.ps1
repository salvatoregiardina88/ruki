#Requires -Version 7
<#
    Pubblica Ruki come applicazione SELF-CONTAINED per Windows x64: un singolo .exe che NON
    richiede l'installazione di .NET (l'utente installa solo Ruki, come da vincoli del progetto).

    Output: publish/app/Ruki.App.exe

    Uso:  pwsh build/publish.ps1
    Poi (facoltativo) build/ruki.iss con Inno Setup per creare l'installer.
#>
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src/Ruki.App/Ruki.App.csproj'
$output = Join-Path $root 'publish/app'

Write-Host "Pubblicazione self-contained (win-x64) in $output ..." -ForegroundColor Cyan

dotnet publish $project `
    -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none -p:DebugSymbols=false `
    -o $output

Write-Host "Fatto. Eseguibile: $(Join-Path $output 'Ruki.App.exe')" -ForegroundColor Green
