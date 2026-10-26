# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Pokekeep contributors
# Tarea de build "construir-bd" (ADR 0005/0007): genera datos\autolegalidad.sqlite DESDE CERO y de forma reproducible a partir de los clones
# fijados por commit (fuentes\), la verifica (integridad + gate del Hito 1) y escribe las cifras reales. El Hito 6 incluye ese fichero en el paquete
# portable PRIVADO; el fichero NO se comitea (esta en .gitignore): va como artefacto de release.
#
# Uso:  powershell -NoProfile -ExecutionPolicy Bypass -File herramientas\construir-bd.ps1 [-Salida <ruta.sqlite>] [-SinTests]
param(
    [string]$Salida = '',
    [switch]$SinTests,
    # Android F2 (plan 5.4): liga la BD a la identidad (SHA-256 de 64 hex) de los binarios del MOVIL en vez de los del PC. Ver construir-bd-android.ps1.
    [string]$CoreSha = '', [string]$EngineSha = '', [string]$AdapterSha = '', [string]$AlmSha = ''
)
. (Join-Path $PSScriptRoot 'entorno.ps1')
$ErrorActionPreference = 'Stop'
if (-not $Salida) { $Salida = Join-Path $RepoRaiz 'datos\autolegalidad.sqlite' }
$rev = "-p:SourceRevisionId=$CoreRev"
$cli = Join-Path $RepoRaiz 'src\AutoLegalidad.Catalog.Cli\AutoLegalidad.Catalog.Cli.csproj'
$dll = Join-Path $RepoRaiz 'src\AutoLegalidad.Catalog.Cli\bin\Release\net10.0\pokekeep-catalogo.dll'
$datos = Join-Path $RepoRaiz 'docs\datos'

Write-Host '=== 1. Fuentes fijadas por commit'
& (Join-Path $PSScriptRoot 'verificar-fuentes.ps1')
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host '=== 2. Compilar (Release, Core determinista)'
& $Dotnet build $cli -c Release $rev --nologo -v q
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host '=== 3. Construir la base de datos'
New-Item -ItemType Directory -Force (Split-Path $Salida) | Out-Null
$extraId = @()
foreach ($par in @(@('--core-sha', $CoreSha), @('--engine-sha', $EngineSha), @('--adapter-sha', $AdapterSha), @('--alm-sha', $AlmSha))) { if ($par[1]) { $extraId += $par[0]; $extraId += $par[1] } }
# Con identidad ajena el informe y el manifiesto del repo no se tocan (son del PC): van junto a la BD.
$informe = if ($extraId.Count -gt 0) { Join-Path (Split-Path $Salida) 'hito1-cifras.json' } else { Join-Path $datos 'hito1-cifras.json' }
$manif = if ($extraId.Count -gt 0) { Join-Path (Split-Path $Salida) 'eventsgallery-manifest.tsv' } else { Join-Path $datos 'eventsgallery-manifest.tsv' }
& $Dotnet $dll construir --repo $RepoRaiz --raiz $Raiz --db $Salida --informe $informe --manifiesto $manif @extraId
$codigoBuild = $LASTEXITCODE

Write-Host '=== 4. Verificacion independiente de la base construida'
& $Dotnet $dll verificar --db $Salida
$codigoVerif = $LASTEXITCODE

Write-Host '=== 5. Rendimiento de la Libreria (mediana/p95 en ms)'
$bench = & $Dotnet $dll libreria-bench --db $Salida
$bench | ForEach-Object { Write-Host $_ }
[IO.File]::WriteAllLines((Join-Path $datos 'libreria-bench.tsv'), [string[]]$bench, (New-Object Text.UTF8Encoding($false)))

$sha = (Get-FileHash -LiteralPath $Salida -Algorithm SHA256).Hash
"$sha  $(Split-Path $Salida -Leaf)" | Set-Content (Join-Path (Split-Path $Salida) 'autolegalidad.sqlite.sha256') -Encoding ascii
Write-Host ("BD: {0} ({1:N1} MB) SHA-256 {2}" -f $Salida, ((Get-Item $Salida).Length / 1MB), $sha)

if (-not $SinTests -and (Test-Path (Join-Path $RepoRaiz 'tests\AutoLegalidad.CatalogTests'))) {   # la exportacion publica no lleva las pruebas
    Write-Host '=== 6. Pruebas de catalogo'
    & $Dotnet test (Join-Path $RepoRaiz 'tests\AutoLegalidad.CatalogTests') -c Release $rev --nologo
    if ($LASTEXITCODE -ne 0) { exit 1 }
}
if ($codigoBuild -ne 0 -or $codigoVerif -ne 0) { Write-Host 'GATE DEL HITO 1 NO CUMPLIDO'; exit 1 }
Write-Host 'construir-bd: OK'
