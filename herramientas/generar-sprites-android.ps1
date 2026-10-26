# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Pokekeep contributors
<#
.SYNOPSIS
  Genera los paquetes de sprites de Android (Fase 3, ADR 0057) a partir de los recursos de PKHeX.Drawing.PokeSprite.
.DESCRIPTION
  Lee fuentes\PKHeX-oficial\PKHeX.Drawing.PokeSprite\Properties\Resources.resx (6.411 recursos) y empaqueta los PNG ORIGINALES, sin recodificar, en un paquete por familia
  (artwork, artwork_shiny, big, big_shiny, arceus, arceus_shiny, items_a, items_b, ball, overlays, status, accents, misc) con los MISMOS nombres de recurso que PKHeX,
  mas manifest.json y procedencia.tsv. Salida por defecto: datos\android\sprites\ (ignorada por git, regenerable y determinista; el APK la incluye desde ahi).
  Con -Verificar no escribe nada: vuelve a leer la salida y comprueba SHA-256 de cada paquete y que cada recurso es identico al PNG de PKHeX.
  Usa el dotnet aislado de Android (entorno-android.ps1) y la ruta corta.
.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File herramientas\generar-sprites-android.ps1
  powershell -NoProfile -ExecutionPolicy Bypass -File herramientas\generar-sprites-android.ps1 -Verificar
#>
param([string]$Salida, [switch]$Verificar)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'entorno-android.ps1')
if (-not $script:AndroidListo) { throw 'Entorno Android incompleto.' }
$repo = $script:RepoRaiz
# Una union por copia de trabajo, como compilar-apk.ps1 (F6/F7): con un nombre unico, una copia de fase compilaria en silencio el arbol de otra.
$nombreCopia = Split-Path $repo -Leaf
$corta = Join-Path $env:TEMP $(if ($nombreCopia -eq 'Pokekeep') { 'pkk-repo' } else { 'pkk-' + ($nombreCopia -replace '^Pokekeep-', '') })
if (-not (Test-Path $corta)) { New-Item -ItemType Junction -Path $corta -Target $repo | Out-Null }
if ((Get-Item $corta).Target -and ((Get-Item $corta).Target | Select-Object -First 1).TrimEnd([char]92) -ne $repo.TrimEnd([char]92)) { throw "La union $corta apunta a otra copia ($((Get-Item $corta).Target)), no a $repo. Quitala (es solo un atajo de ruta) y vuelve a generar." }
if (-not $Salida) { $Salida = Join-Path $repo 'datos\android\sprites' }
$resx = Join-Path $repo 'fuentes\PKHeX-oficial\PKHeX.Drawing.PokeSprite\Properties\Resources.resx'
$resxMisc = Join-Path $repo 'fuentes\PKHeX-oficial\PKHeX.Drawing.Misc\Properties\Resources.resx'   # fondos de caja (F4)
if (-not (Test-Path $resx)) { throw "No existe $resx (fuentes\ de PKHeX)." }
if (-not (Test-Path $resxMisc)) { throw "No existe $resxMisc (fuentes\ de PKHeX)." }
$proyecto = Join-Path $corta 'android\Pokekeep.Sprites.Build\Pokekeep.Sprites.Build.csproj'
$modo = if ($Verificar) { 'verificar' } else { 'generar' }
$old = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
& $script:DotnetAndroid build $proyecto -c Release --nologo -v q
$codigo = $LASTEXITCODE
if ($codigo -eq 0) {
    $dll = Get-ChildItem (Join-Path $repo 'android\Pokekeep.Sprites.Build\bin\Release') -Recurse -Filter 'Pokekeep.Sprites.Build.dll' | Select-Object -First 1
    & $script:DotnetAndroid $dll.FullName $modo --resx $resx --resx-misc $resxMisc --salida $Salida --origen 'PKHeX.Drawing.PokeSprite (fuentes PKHeX-oficial)'
    $codigo = $LASTEXITCODE
}
$ErrorActionPreference = $old
if ($codigo -ne 0) { throw "El generador de sprites fallo (codigo $codigo)." }
