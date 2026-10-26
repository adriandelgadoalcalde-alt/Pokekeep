# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Pokekeep contributors
<#
.SYNOPSIS
  Construye el paquete PORTABLE de Pokekeep (Hito 6, R-008, ADR 0036): distribucion\PKHeX-Pokekeep\ y, con -Zip, distribucion\PKHeX-Pokekeep-<version>.zip.
.DESCRIPTION
  1. Asegura la copia de PKHeX con parches (herramientas\construir-winforms.ps1 -SoloCopia) y publica PKHeX.exe y el Worker AUTOCONTENIDOS (.NET 10, win-x64, sin .NET instalado)
     en la MISMA carpeta: un unico PKHeX.Core.dll compartido (se comprueba que no hay otro), un unico runtime. Compilacion reproducible: -p:SourceRevisionId fijo y PathMap
     (ningun binario lleva la ruta de la maquina de construccion; los .pdb no se incluyen).
  2. Copia la base de datos ya construida (construir-bd.ps1) y, si existe, la cache de soluciones (soluciones.sqlite), las licencias, el aviso de EventsGallery, la guia (LEEME.txt),
     el verificador y el codigo fuente correspondiente (GPL).
  3. Escribe Pokekeep\manifiesto.json (SHA-256 de CADA fichero; su presencia activa el modo portable) y MANIFIESTO-SHA256.txt.
  4. Con -Zip crea el ZIP (7-Zip de dev-tools) con una carpeta raiz PKHeX-Pokekeep\ y devuelve su SHA-256, y genera el informe de cobertura publicable (distribucion\INFORME-COBERTURA-<version>.md/.html, ADR 0050).
  La BD, la cache y el ZIP son artefactos de release (ADR 0005 y 0104): estan en .gitignore y NO se suben al repo, solo a la release.
#>
param(
    [string]$Version = '1.0.1',
    [string]$Salida,
    [string]$BD,
    [string]$Soluciones,
    [string]$NombreCopia = 'PKHeX-Pokekeep-h6',
    [switch]$Zip,
    [switch]$SinReconstruir,
    [switch]$SinR2R,
    [switch]$ReconstruirBD,
    [switch]$SinInforme,    # con -Zip, no genera el informe de cobertura (necesita las mediciones de docs\datos, que la exportacion publica no lleva)
    [switch]$ParaRegistro   # NO es el paquete: mismos binarios de motor (mismo hash) pero con el soporte de desarrollo del registro de soluciones; salida en distribucion\_registro
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'entorno.ps1')
$Utf8 = New-Object System.Text.UTF8Encoding($false)
if (-not $Salida) { $Salida = Join-Path $RepoRaiz $(if ($ParaRegistro) { 'distribucion\_registro' } else { 'distribucion\PKHeX-Pokekeep' }) }
if (-not $BD) { $BD = Join-Path $RepoRaiz 'datos\portable\autolegalidad.sqlite' }   # BD construida con EL Core del paquete (el catalogo enlaza el hash del Core): distinta de la de desarrollo
if (-not $Soluciones) { $Soluciones = Join-Path $RepoRaiz 'datos\portable\soluciones.sqlite' }
$copia = Join-Path $Raiz (Join-Path 'fuentes' $NombreCopia)
$tmp = Join-Path $RepoRaiz $(if ($ParaRegistro) { 'distribucion\_construccion-registro' } else { 'distribucion\_construccion' })
$Portable = if ($ParaRegistro) { 'false' } else { 'true' }
$siete = $SieteZip
$R2R = if ($SinR2R) { 'false' } else { 'true' }

function Run([string]$what, [scriptblock]$cmd) {
    Write-Host "== $what ==" -ForegroundColor Cyan
    $old = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    $global:LASTEXITCODE = 0
    & $cmd
    $code = $LASTEXITCODE
    $ErrorActionPreference = $old
    if ($code -ne 0) { throw "Fallo: $what (codigo $code)" }
}


# --- 1. PKHeX con parches + Worker, autocontenidos ---
Run 'copia de PKHeX con parches' { & (Join-Path $PSScriptRoot 'construir-winforms.ps1') -NombreCopia $NombreCopia -SoloCopia }
$core = Join-Path $copia 'PKHeX.Core\PKHeX.Core.csproj'
$ui = Join-Path $RepoRaiz 'src\AutoLegalidad.Ui\AutoLegalidad.Ui.csproj'
# Reproducible y sin rutas de la maquina: PathMap reescribe TODA ruta bajo <raiz> (fuentes, repo y cache NuGet) en los binarios; sin .pdb.
$comunes = @('-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', "-p:PublishReadyToRun=$R2R", '-p:PublishSingleFile=false', '-p:PublishTrimmed=false',
    "-p:SourceRevisionId=$CoreRev", "-p:PKHeXCoreProject=$core", "-p:PokekeepUiProject=$ui", '-p:DebugType=none', '-p:EmbedAllSources=false', "-p:PokekeepPortable=$Portable", '-p:DebugSymbols=false',
    "-p:PathMap=$Raiz=/pokekeep", '-p:ContinuousIntegrationBuild=true', '-p:SatelliteResourceLanguages=es%3Ben', '-m:1')
# 1.0.0: -m:1 porque desde la 0.7.0 AutoLegalidad.App lleva el motor y WorkerHost llega a PKHeX.WinForms por dos caminos con propiedades distintas;
# en paralelo MSBuild lo compilaba dos veces a la vez y las dos escrituras chocaban en obj\ (CS2012, «being used by another process»). En serie, mismo resultado.
if (-not $SinReconstruir -and (Test-Path $tmp)) { cmd /c "rmdir /s /q `"$tmp`"" }
New-Item -ItemType Directory -Force $tmp | Out-Null
$wf = Join-Path $tmp 'winforms'; $wk = Join-Path $tmp 'worker'; $cliDir = Join-Path $tmp 'catalogo'
if (-not $SinReconstruir) {
    Run 'publicar PKHeX.WinForms (autocontenido)' { & $Dotnet publish (Join-Path $copia 'PKHeX.WinForms\PKHeX.WinForms.csproj') @comunes -o $wf }
    Run 'publicar el Worker (autocontenido)' { & $Dotnet publish (Join-Path $RepoRaiz 'src\AutoLegalidad.Worker\AutoLegalidad.Worker.csproj') @comunes -o $wk }
    if (-not $ParaRegistro) { Run 'publicar el constructor del catalogo (mismo Core)' { & $Dotnet publish (Join-Path $RepoRaiz 'src\AutoLegalidad.Catalog.Cli\AutoLegalidad.Catalog.Cli.csproj') @comunes -o $cliDir } }
}

# Los lockfiles de NuGet ganan entradas de win-x64 al publicar: se restauran para no ensuciar el repo.
$old = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
& $Git -C $RepoRaiz checkout -- ':(glob)**/packages.lock.json' 2>&1 | Out-Null
$ErrorActionPreference = $old

# --- 2. Montaje: UNA carpeta, UN PKHeX.Core.dll ---
if (Test-Path $Salida) { cmd /c "rmdir /s /q `"$Salida`"" }
New-Item -ItemType Directory -Force $Salida | Out-Null
Copy-Item (Join-Path $wf '*') $Salida -Recurse -Force
$avisos = @()
foreach ($f in Get-ChildItem $wk -Recurse -File) {
    $rel = $f.FullName.Substring($wk.Length).TrimStart('\')
    $dest = Join-Path $Salida $rel
    if (Test-Path $dest) {
        $a = (Get-FileHash $f.FullName -Algorithm SHA256).Hash; $b = (Get-FileHash $dest -Algorithm SHA256).Hash
        if ($a -ne $b) { $avisos += "difiere (se conserva el de PKHeX.WinForms): $rel" }
        continue
    }
    New-Item -ItemType Directory -Force (Split-Path $dest) | Out-Null
    Copy-Item $f.FullName $dest
}
$avisos | ForEach-Object { Write-Host "  aviso: $_" -ForegroundColor Yellow }
$cores = @(Get-ChildItem $Salida -Recurse -File -Filter 'PKHeX.Core.dll')
if ($cores.Count -ne 1) { throw "Debe haber UN solo PKHeX.Core.dll en el paquete y hay $($cores.Count)." }
if ($cores[0].DirectoryName -ne (Resolve-Path $Salida).Path) { throw 'El PKHeX.Core.dll debe estar junto a PKHeX.exe.' }
foreach ($n in 'PKHeX.exe', 'pokekeep-worker.exe', 'AutoLegalidad.Engine.dll', 'AutoLegalidad.Ui.dll') { if (-not (Test-Path (Join-Path $Salida $n))) { throw "Falta $n en el paquete." } }
Get-ChildItem $Salida -Recurse -File -Include '*.pdb', '*.xml' | Where-Object { $_.Name -notlike '*.deps.json' } | Remove-Item
# ADR 0040: el registro de fallos del usuario (registros\) nunca viaja en el paquete.
if (Test-Path (Join-Path $Salida 'registros')) { cmd /c "rmdir /s /q `"$(Join-Path $Salida 'registros')`"" }

if ($ParaRegistro) { Write-Host "Carpeta de registro lista: $Salida" -ForegroundColor Green; return }

# --- 2b. Base de datos del catalogo construida con EL Core del paquete ---
# El catalogo (snapshot verificado) enlaza el SHA-256 del PKHeX.Core.dll con el que se construyo y el Worker se niega a trabajar con otro: la BD del paquete se construye con el MISMO
# fichero (el constructor se publica con las mismas opciones y se comprueba que su Core es byte a byte el del paquete).
$cliCore = (Get-FileHash (Join-Path $cliDir 'PKHeX.Core.dll') -Algorithm SHA256).Hash
$pkgCore = (Get-FileHash (Join-Path $Salida 'PKHeX.Core.dll') -Algorithm SHA256).Hash
if ($cliCore -ne $pkgCore) { throw "El PKHeX.Core.dll del constructor del catalogo ($cliCore) no es el del paquete ($pkgCore)." }
if ($ReconstruirBD -or -not (Test-Path $BD)) {
    Run 'verificar fuentes fijadas' { & (Join-Path $PSScriptRoot 'verificar-fuentes.ps1') }
    New-Item -ItemType Directory -Force (Split-Path $BD) | Out-Null
    foreach ($x in '', '-wal', '-shm') { if (Test-Path "$BD$x") { Remove-Item "$BD$x" } }
    $cliExe = Join-Path $cliDir 'pokekeep-catalogo.exe'
    $cifras = Join-Path $tmp 'cifras-hito1.json'; $egManifest = Join-Path $tmp 'eventsgallery-manifest.tsv'
    Run 'construir la base de datos (Core del paquete)' { & $cliExe construir --repo $RepoRaiz --raiz $Raiz --db $BD --informe $cifras --manifiesto $egManifest }
    Run 'verificar la base de datos' { & $cliExe verificar --db $BD }
    "$((Get-FileHash $BD -Algorithm SHA256).Hash)  autolegalidad.sqlite" | Set-Content (Join-Path (Split-Path $BD) 'autolegalidad.sqlite.sha256') -Encoding ascii
}
if (-not (Test-Path $BD)) { throw "Falta la base de datos $BD." }
if (Test-Path "$BD-wal") { throw "La base de datos tiene un -wal pendiente: $BD-wal" }

$dataDir = Join-Path $Salida 'Pokekeep\datos'
New-Item -ItemType Directory -Force $dataDir | Out-Null
Copy-Item $BD (Join-Path $dataDir 'autolegalidad.sqlite')
if (Test-Path $Soluciones) { Copy-Item $Soluciones (Join-Path $dataDir 'soluciones.sqlite'); Write-Host "  cache de soluciones incluida ($((Get-Item $Soluciones).Length / 1MB) MB)" }
else { Write-Host '  (sin cache de soluciones: datos\soluciones.sqlite no existe)' -ForegroundColor Yellow }

# --- 3. Licencias, avisos, guia, verificador y fuente correspondiente ---
$plantilla = Join-Path $RepoRaiz 'distribucion\plantilla'
$lic = Join-Path $Salida 'Licencias'
New-Item -ItemType Directory -Force $lic | Out-Null
Copy-Item (Join-Path $RepoRaiz 'LICENSE') (Join-Path $lic 'GPL-3.0-Pokekeep-y-PKHeX.txt')
Copy-Item (Join-Path $RepoRaiz 'NOTICE.md') (Join-Path $lic 'NOTICE.md')
$almLic = Join-Path $Raiz 'fuentes\ALM-estable\LICENSE'
if (Test-Path $almLic) { Copy-Item $almLic (Join-Path $lic 'MIT-ALM-PKHeX-Plugins.txt') } else { throw "No encuentro la licencia MIT de ALM ($almLic)." }
$pack = Join-Path $env:NUGET_PACKAGES 'microsoft.netcore.app.runtime.win-x64'
$packDir = Get-ChildItem $pack -Directory | Sort-Object Name | Select-Object -Last 1
foreach ($n in 'LICENSE.txt', 'THIRD-PARTY-NOTICES.TXT') { $s = Join-Path $packDir.FullName $n; if (Test-Path $s) { Copy-Item $s (Join-Path $lic ("DotNet-" + $n)) } }
$Bom = New-Object System.Text.UTF8Encoding($true)   # los textos con tildes llevan BOM: Windows PowerShell 5.1 y el Bloc de notas los leen bien
foreach ($n in 'AVISOS-DE-TERCEROS.txt', 'AVISO-EventsGallery.txt') { [IO.File]::WriteAllText((Join-Path $lic $n), (Get-Content (Join-Path $plantilla $n) -Raw -Encoding UTF8), $Bom) }

$git = $Git
$commit = (& $git -C $RepoRaiz rev-parse HEAD).Trim()
$rama = (& $git -C $RepoRaiz rev-parse --abbrev-ref HEAD).Trim()
$fuenteDir = Join-Path $Salida 'Fuente'
New-Item -ItemType Directory -Force $fuenteDir | Out-Null
$old = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
& $git -C $RepoRaiz archive --format=zip -o (Join-Path $fuenteDir 'Pokekeep-fuente.zip') HEAD -- . ':(exclude)docs' ':(exclude)bitacora.md' ':(exclude)CLAUDE.md' ':(exclude)HECHO-raids.txt' ':(exclude)HECHO-transfer.txt' ':(exclude)herramientas/auditar-privacidad.ps1' ':(exclude)herramientas/auditar-rutas-portable.py' ':(exclude)herramientas/sanear-logs.ps1' ':(exclude)tests/AutoLegalidad.CatalogTests/PrivacidadTests.cs'
$archiveCode = $LASTEXITCODE; $ErrorActionPreference = $old
if ($archiveCode -ne 0) { throw 'git archive fallo.' }
$srcText = (Get-Content (Join-Path $plantilla 'FUENTE-CORRESPONDIENTE.txt') -Raw -Encoding UTF8).Replace('{{COMMIT}}', $commit).Replace('{{RAMA}}', $rama).Replace('{{VERSION}}', $Version).Replace('{{CORE}}', '26.10.18 (bfe89c74a3f5265805e364b79bc04ad38ebbc5ea)')
[IO.File]::WriteAllText((Join-Path $fuenteDir 'LEEME-FUENTE.txt'), $srcText, $Bom)

$leeme = (Get-Content (Join-Path $plantilla 'LEEME.txt') -Raw -Encoding UTF8).Replace('{{VERSION}}', $Version)
[IO.File]::WriteAllText((Join-Path $Salida 'LEEME.txt'), $leeme, $Bom)
[IO.File]::WriteAllText((Join-Path $Salida 'Verificar-paquete.ps1'), (Get-Content (Join-Path $plantilla 'Verificar-paquete.ps1') -Raw -Encoding UTF8), $Bom)
Copy-Item (Join-Path $plantilla 'cfg.json') (Join-Path $Salida 'cfg.json')

# --- 4. Manifiesto con SHA-256 de cada fichero (su presencia activa el modo portable) ---
Write-Host '== manifiesto SHA-256 ==' -ForegroundColor Cyan
$excl = @('Pokekeep\manifiesto.json', 'MANIFIESTO-SHA256.txt', 'cfg.json')   # cfg.json lo reescribe PKHeX al cerrar: no es un fichero fijo del paquete
$files = Get-ChildItem $Salida -Recurse -File | Where-Object { $rel = $_.FullName.Substring((Resolve-Path $Salida).Path.Length).TrimStart('\'); $excl -notcontains $rel } | Sort-Object FullName
$base = (Resolve-Path $Salida).Path
$entries = New-Object System.Collections.Generic.List[object]
$txt = New-Object System.Text.StringBuilder
foreach ($f in $files) {
    $rel = $f.FullName.Substring($base.Length).TrimStart('\').Replace('\', '/')
    $h = (Get-FileHash $f.FullName -Algorithm SHA256).Hash
    $entries.Add([ordered]@{ ruta = $rel; bytes = $f.Length; sha256 = $h })
    [void]$txt.Append("$h *$rel`n")
}
$coreHash = (Get-FileHash (Join-Path $Salida 'PKHeX.Core.dll') -Algorithm SHA256).Hash
$manifest = [ordered]@{
    producto = 'PKHeX-Pokekeep'
    version = $Version
    generado = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    pareja = [ordered]@{ pkhex = '26.10.18'; pkhexCommit = 'bfe89c74a3f5265805e364b79bc04ad38ebbc5ea'; alm = '26.10.18'; almCommit = 'b13c011bb0c4c3d475ca7f0a30b880f5b24dbed7'; motor = (Select-String -Path (Join-Path $RepoRaiz 'src\AutoLegalidad.Engine\PinnedVersions.cs') -Pattern 'EngineVersion = "([^"]+)"').Matches[0].Groups[1].Value }
    core = [ordered]@{ archivo = 'PKHeX.Core.dll'; sha256 = $coreHash }
    archivos = $entries
}
$json = $manifest | ConvertTo-Json -Depth 6
[IO.File]::WriteAllText((Join-Path $Salida 'Pokekeep\manifiesto.json'), $json, $Utf8)
[IO.File]::WriteAllText((Join-Path $Salida 'MANIFIESTO-SHA256.txt'), $txt.ToString(), $Utf8)
$total = ($files | Measure-Object Length -Sum).Sum
Write-Host ("Paquete: {0} ficheros, {1:N1} MB, PKHeX.Core.dll {2}" -f $files.Count, ($total / 1MB), $coreHash) -ForegroundColor Green

# --- 5. ZIP ---
if ($Zip) {
    $zipPath = Join-Path $RepoRaiz "distribucion\PKHeX-Pokekeep-$Version.zip"
    if (Test-Path $zipPath) { Remove-Item $zipPath }
    Run 'ZIP (7-Zip)' { Push-Location (Split-Path $Salida); & $siete a -tzip -mx=6 -mmt=on '-xr!registros' $zipPath (Split-Path $Salida -Leaf) | Out-Null; Pop-Location }
    $zh = (Get-FileHash $zipPath -Algorithm SHA256).Hash
    [IO.File]::WriteAllText("$zipPath.sha256", "$zh *$(Split-Path $zipPath -Leaf)`n", $Utf8)
    Write-Host ("ZIP: {0} ({1:N1} MB) SHA-256 {2}" -f $zipPath, ((Get-Item $zipPath).Length / 1MB), $zh) -ForegroundColor Green
    # ADR 0050: informe de cobertura publicable para adjuntar a la release (distribucion\INFORME-COBERTURA-<version>.md/.html; matriz con estados, sin datos personales).
    if (-not $SinInforme) { & (Join-Path $PSScriptRoot 'generar-informe-cobertura.ps1') -Version $Version -BD $BD }
}
