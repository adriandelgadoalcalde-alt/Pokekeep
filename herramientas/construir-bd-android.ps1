# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Pokekeep contributors
<#
.SYNOPSIS
  Construye en el PC el catalogo y la cache de soluciones de ANDROID (plan 5.4, F2): datos\android\autolegalidad.sqlite y datos\android\soluciones.sqlite, ligados a la identidad de los binarios del movil.
.DESCRIPTION
  En Android Assembly.Location esta vacio y los ensamblados viajan dentro del APK: su identidad (SHA-256 de PKHeX.Core, motor, adaptador de ALM y fuente de ALM) la calcula la compilacion (target GenerarIdentidad
  de Pokekeep.Mobile.Engine) y la incrusta. Este script: 1) compila ESE proyecto con los mismos parametros que el APK y lee la identidad, 2) construye el catalogo desde cero con --core-sha/--engine-sha/--adapter-sha/--alm-sha
  (herramientas\construir-bd.ps1), 3) sella una copia de la cache de soluciones del paquete con la misma identidad y el id de la nueva instantanea del catalogo (herramientas\sellar-soluciones.py). El contenido de la cache
  (claves y soluciones: semillas e IV) no depende de la plataforma; solo cambia la identidad que la liga. La prueba de que es asi es el banco de 50 claves de F2 (identidad.json): el motor Android sirve las mismas claves.
  Si el APK se recompila y cambia el hash de algun binario, hay que volver a ejecutarlo (el motor lo diria: VersionMismatch).
#>
param(
    [string]$Rid = 'android-x64',
    # X42 (ADR 0098): 0 = catalogo tal cual sale de la construccion (por defecto, hasta que el propietario decida); 1 o 2 = lo deja ademas en su forma compacta para el movil
    # (mismas filas y columnas, otro almacenamiento: nivel 1 -26 %, nivel 2 -51 %). Ver docs\android\seguimiento-cierre-codex.md, seccion X42.
    [ValidateSet(0, 1, 2)][int]$Compactar = 0,
    # Release: SOLO cambio el motor (el Core y las fuentes del catalogo son los mismos): no se reconstruye el catalogo (el catalogo solo lleva core_dll_sha256, no el del motor) y la cache de
    # soluciones se re-sella desde la que ya hay en datos\android (ya en formato v2). Reescribe identidad.json con la nueva y vuelve a sellar. Con -Compactar compacta el catalogo existente
    # (que debe estar sin compactar). No exige datos\portable.
    [switch]$Resellar,
    # Exportacion publica: la cache de soluciones se precalcula con el banco de pruebas (construir-soluciones.ps1), que no se publica. Con -SinCache se construye solo el catalogo y la
    # identidad; el APK sale sin soluciones precalculadas (el motor las calcula al vuelo, mas despacio). Sin este interruptor, la falta de cache sigue siendo un error.
    [switch]$SinCache
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'entorno.ps1')
$raiz = $RepoRaiz
$salida = Join-Path $raiz 'datos\android'
New-Item -ItemType Directory -Force $salida | Out-Null

Write-Host '=== 1. Identidad de los binarios del movil'
$linea = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'compilar-apk.ps1') -SoloIdentidad -Rid $Rid | Select-Object -Last 1
$id = $linea | ConvertFrom-Json
if (-not $id.Core -or -not $id.Engine -or -not $id.Adapter -or -not $id.Alm) { throw "No se pudo leer la identidad: $linea" }
$id | ConvertTo-Json | Set-Content (Join-Path $salida 'identidad.json') -Encoding utf8
Write-Host ("Core {0}  Motor {1}  Adaptador {2}  ALM {3}" -f $id.Core.Substring(0, 12), $id.Engine.Substring(0, 12), $id.Adapter.Substring(0, 12), $id.Alm.Substring(0, 12))

Write-Host '=== 2. Catalogo de Android'
if ($Resellar) {
    $actual = Join-Path $salida 'autolegalidad.sqlite'
    if (-not (Test-Path $actual)) { throw "-Resellar necesita el catalogo ya construido en $actual." }
    Write-Host 'Catalogo existente conservado (-Resellar): no depende del hash del motor.'
    $global:LASTEXITCODE = 0
} else {
& (Join-Path $PSScriptRoot 'construir-bd.ps1') -Salida (Join-Path $salida 'autolegalidad.sqlite') -SinTests -CoreSha $id.Core -EngineSha $id.Engine -AdapterSha $id.Adapter -AlmSha $id.Alm
if ($LASTEXITCODE -ne 0) { throw 'construir-bd fallo.' }
}

if ($Compactar -gt 0) {
    Write-Host "=== 2b. Catalogo compacto para el movil (nivel $Compactar, ADR 0098)"
    $cli = Join-Path $raiz 'src\AutoLegalidad.Catalog.Cli\bin\Release\net10.0\pokekeep-catalogo.dll'
    $bd = Join-Path $salida 'autolegalidad.sqlite'
    if ($Resellar -and -not (Test-Path $cli)) {
        & $Dotnet build (Join-Path $raiz 'src\AutoLegalidad.Catalog.Cli\AutoLegalidad.Catalog.Cli.csproj') -c Release "-p:SourceRevisionId=$CoreRev" --nologo -v q
        if ($LASTEXITCODE -ne 0) { throw 'No compila el CLI del catalogo.' }
    }
    & $Dotnet $cli compactar-movil --db $bd --nivel $Compactar
    if ($LASTEXITCODE -ne 0) { throw 'compactar-movil fallo.' }
    & $Dotnet $cli verificar --db $bd
    if ($LASTEXITCODE -ne 0) { throw 'El catalogo compacto no supera la verificacion.' }
    & $Dotnet $cli libreria-bench --db $bd | ForEach-Object { Write-Host $_ }
    # construir-bd.ps1 escribio la huella ANTES de compactar: se rehace con el fichero definitivo.
    $sha = (Get-FileHash -LiteralPath $bd -Algorithm SHA256).Hash
    "$sha  autolegalidad.sqlite" | Set-Content (Join-Path $salida 'autolegalidad.sqlite.sha256') -Encoding ascii
    Write-Host ("BD compacta: {0:N1} MB, SHA-256 {1}" -f ((Get-Item $bd).Length / 1MB), $sha)
}

if ($SinCache) {
    $viejaCache = Join-Path $salida 'soluciones.sqlite'
    if (Test-Path $viejaCache) { Remove-Item $viejaCache -Force }   # una cache de otra identidad nunca debe viajar con este catalogo
    Write-Host '=== 3. Sin cache de soluciones (-SinCache): el APK saldra sin soluciones precalculadas' -ForegroundColor Yellow
    Write-Host 'datos\android listo (solo catalogo e identidad).' -ForegroundColor Green
    return
}
Write-Host '=== 3. Cache de soluciones de Android (sellada con la misma identidad)'
$origen = if ($Resellar) { Join-Path $salida 'soluciones.sqlite' } else { Join-Path $raiz 'datos\portable\soluciones.sqlite' }
if (-not (Test-Path $origen)) { throw "Falta la cache del paquete ($origen): herramientas\construir-soluciones.ps1." }
# X43 (ADR 0097): la cache viaja al movil en formato v2 (binario, ~40 % menos). Se convierte a un fichero temporal (con la verificacion fila a fila) y ESE es el que se sella; el original no se toca.
# Si el origen ya es v2, la conversion es la identidad (mismas filas, mismos bytes de contenido).
$v2 = Join-Path $salida 'soluciones-v2.tmp.sqlite'
if (Test-Path $v2) { Remove-Item $v2 -Force }
if ($Resellar) { Copy-Item $origen $v2 -Force }   # ya es v2: se re-sella con la identidad nueva
else {
& (Join-Path $PSScriptRoot 'convertir-soluciones-v2.ps1') -Origen $origen -Destino $v2 -Compilar | ForEach-Object { Write-Host $_ }
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $v2)) { throw 'convertir-soluciones-v2 fallo.' }
}
& $Python (Join-Path $PSScriptRoot 'sellar-soluciones.py') --origen $v2 --destino (Join-Path $salida 'soluciones.sqlite') --catalogo (Join-Path $salida 'autolegalidad.sqlite') `
    --core $id.Core --engine $id.Engine --adapter $id.Adapter --alm $id.Alm
if ($LASTEXITCODE -ne 0) { throw 'sellar-soluciones fallo.' }
Remove-Item $v2 -Force
Write-Host 'datos\android listo.' -ForegroundColor Green
