# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Pokekeep contributors
<#
.SYNOPSIS
  Compila el PKHeX.WinForms de Pokekeep (Hito 4): copia de PKHeX 26.10.18 + parches propios (carpeta parches\) + AutoLegalidad.Ui + Worker.
.DESCRIPTION
  La copia vive en <raiz>\fuentes\PKHeX-Pokekeep (rama pokekeep/hito-4, mismo commit base que fuentes\PKHeX-oficial, que NO se toca; ADR 0016).
  Si no existe se crea con `git clone` local y se le aplican los parches con `git am`. Con -ExportarParches se regeneran parches\*.patch desde la rama.
  El Core se compila SIEMPRE con -p:SourceRevisionId=<commit fijado> (ADR 0003). Salida: <raiz>\compilacion\winforms-pokekeep\PKHeX.exe
  (el motor y el catalogo se localizan con POKEKEEP_WORKER / POKEKEEP_DB o en la carpeta Pokekeep junto al ejecutable; el empaquetado final es del Hito 6).
#>
param([switch]$ExportarParches, [string]$Salida, [string]$NombreCopia = 'PKHeX-Pokekeep', [switch]$SoloCopia)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'entorno.ps1')
$base = 'bfe89c74a3f5265805e364b79bc04ad38ebbc5ea'
$copia = Join-Path $Raiz (Join-Path 'fuentes' $NombreCopia)   # -NombreCopia: una copia propia por rama/worktree evita pisar los obj\ de otra sesion (Hito 6: 'PKHeX-Pokekeep-h6')
$parches = Join-Path $RepoRaiz 'parches'
if (-not $Salida) { $Salida = Join-Path $Raiz 'compilacion\winforms-pokekeep' }

if (-not (Test-Path (Join-Path $copia '.git'))) {
    Write-Host "Creando la copia de PKHeX con parches en $copia" -ForegroundColor Cyan
    & $Git config --global core.longpaths true
    & $Git clone --no-checkout --no-hardlinks (Join-Path $Raiz 'fuentes\PKHeX-oficial') $copia
    Push-Location $copia
    & $Git sparse-checkout init --cone
    & $Git sparse-checkout set PKHeX.Core PKHeX.Drawing PKHeX.Drawing.Misc PKHeX.Drawing.PokeSprite PKHeX.WinForms
    & $Git checkout -b pokekeep/hito-4 $base
    & $Git config user.name 'Pokekeep'
    & $Git config user.email 'pokekeep@invalid'
    if (Test-Path $parches) {
        $lista = Get-ChildItem $parches -Filter '*.patch' | Sort-Object Name
        if ($lista) { & $Git am --3way @($lista.FullName) }
    }
    Pop-Location
}

if ($ExportarParches) {
    Push-Location $copia
    New-Item -ItemType Directory -Force $parches | Out-Null
    Get-ChildItem $parches -Filter '*.patch' | Remove-Item
    & $Git format-patch "$base..HEAD" -o $parches
    Pop-Location
    Write-Host "Parches exportados a $parches" -ForegroundColor Green
}

if ($SoloCopia) { Write-Host "Copia lista: $copia" -ForegroundColor Green; return }

Write-Host '== Worker (Release) ==' -ForegroundColor Cyan
& $Dotnet build (Join-Path $RepoRaiz 'src\AutoLegalidad.Worker') -c Release "-p:SourceRevisionId=$CoreRev"
if ($LASTEXITCODE -ne 0) { throw 'Fallo la compilacion del Worker.' }

Write-Host '== PKHeX.WinForms de Pokekeep (Release) ==' -ForegroundColor Cyan
$core = Join-Path $copia 'PKHeX.Core\PKHeX.Core.csproj'
& $Dotnet build (Join-Path $copia 'PKHeX.WinForms\PKHeX.WinForms.csproj') -c Release "-p:SourceRevisionId=$CoreRev" "-p:PKHeXCoreProject=$core" "-p:PokekeepUiProject=$(Join-Path $RepoRaiz 'src\AutoLegalidad.Ui\AutoLegalidad.Ui.csproj')" -o $Salida
if ($LASTEXITCODE -ne 0) { throw 'Fallo la compilacion de PKHeX.WinForms.' }
Write-Host "Listo: $Salida\PKHeX.exe" -ForegroundColor Green
