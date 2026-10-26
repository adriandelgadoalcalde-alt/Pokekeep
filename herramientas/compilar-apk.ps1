# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Pokekeep contributors
<#
.SYNOPSIS
  Compila el APK de Pokekeep para Android (Fase 2). Release por defecto; arm64 (artefacto) o x86_64 (emulador). Usa el entorno aislado y la ruta corta (XARLP7000).
.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File herramientas\compilar-apk.ps1 -Rid android-x64 -Demo
.NOTES
  -Demo incluye una COPIA de un guardado de la coleccion publica RoC como asset (solo para comprobar el diseno en el emulador; nunca en el producto).
  -ArtifactsPath permite compilar con fuentes de solo lectura: restauracion, intermedios y binarios se escriben en esa carpeta.
#>
param([ValidateSet('android-arm64', 'android-x64')][string]$Rid = 'android-x64', [string]$Configuracion = 'Release', [switch]$Demo, [switch]$Pruebas, [switch]$SoloIdentidad, [switch]$Recompilar, [string]$Salida, [string]$ArtifactsPath)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'entorno-android.ps1')
. (Join-Path $PSScriptRoot 'lib-gate.ps1')
if (-not $script:AndroidListo) { throw 'Entorno Android incompleto.' }
$repo = $script:RepoRaiz
# Ruta corta por la union de entorno-android.ps1 (XARLP7000): el proyecto se compila a traves de ella.
# Una union por copia de trabajo (la principal conserva 'pkk-repo'; Pokekeep-f6 usa 'pkk-f6'): con un nombre unico las fases en paralelo compilarian el arbol de OTRA copia (F6, ADR 0060).
$nombreCopia = Split-Path $repo -Leaf
$corta = Join-Path $env:TEMP $(if ($nombreCopia -eq 'Pokekeep') { 'pkk-repo' } else { 'pkk-' + ($nombreCopia -replace '^Pokekeep-', '') })
if (-not (Test-Path $corta)) { New-Item -ItemType Junction -Path $corta -Target $repo | Out-Null }
# Nunca compilar en silencio el arbol de otra copia (F7): si la union existe y apunta a otro sitio, se para.
if ((Get-Item $corta).Target -and ((Get-Item $corta).Target | Select-Object -First 1).TrimEnd([char]92) -ne $repo.TrimEnd([char]92)) { throw "La union $corta apunta a otra copia ($((Get-Item $corta).Target)), no a $repo. Quitala (es solo un atajo de ruta) y vuelve a compilar." }
$csproj = Join-Path $corta $(if ($SoloIdentidad) { 'android\Pokekeep.Mobile.Engine\Pokekeep.Mobile.Engine.csproj' } else { 'android\Pokekeep.Mobile\Pokekeep.Mobile.csproj' })
$args2 = @('build', $csproj, '-c', $Configuracion, '-r', $Rid, '--self-contained', "-p:PokekeepRid=$Rid", '-p:PokekeepAndroid=true', '-p:TreatWarningsAsErrors=false', "-p:PathMap=$($corta.TrimEnd([char]92))$([char]92)=/pokekeep/", '-p:DebugType=none', '-p:DebugSymbols=false', '-p:Deterministic=true', '-p:SourceRevisionId=bfe89c74a3f5', '-p:AndroidPackageFormat=apk', '-p:EmbedAssembliesIntoApk=true',
    "-p:AndroidSdkDirectory=$script:AndroidSdkDirectory", "-p:JavaSdkDirectory=$script:JavaSdkDirectory", '-m:1', '-nr:false', '-p:UsedAvaloniaProducts=', '--nologo', '-v', 'q')
if ($Demo) { $args2 += '-p:PokekeepDemo=true' }
if ($Pruebas) { $args2 += '-p:PokekeepPruebas=true' }
if ($Recompilar) { $args2 += '-t:Rebuild' }
if ($ArtifactsPath) {
    $ArtifactsPath = [IO.Path]::GetFullPath($ArtifactsPath)
    $args2 += @('--artifacts-path', $ArtifactsPath)
}
$motorPivot = $Configuracion.ToLowerInvariant()
if ($SoloIdentidad) { $motorPivot += '_' + $Rid }
$motorObj = if ($ArtifactsPath) { Join-Path $ArtifactsPath ("obj\Pokekeep.Mobile.Engine\" + $motorPivot) } else { Join-Path $repo 'android\Pokekeep.Mobile.Engine\obj' }
$old = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
& $script:DotnetAndroid @args2
$codigo = $LASTEXITCODE
$ErrorActionPreference = $old
if ($codigo -ne 0) { throw "La compilacion fallo (codigo $codigo)." }
if ($SoloIdentidad) {
    # Identidad de los cuatro binarios de ESTA compilacion (los que viajaran en el APK): la BD y la cache de Android se ligan a estos valores.
    $f = Get-ChildItem $motorObj -Recurse -Filter 'Identidad.g.cs' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    $t = Get-Content $f.FullName -Raw
    $r = [ordered]@{}
    foreach ($k in 'Core', 'Engine', 'Adapter', 'Alm') { $r[$k] = [regex]::Match($t, "Pokekeep\.${k}Sha256\W+([0-9A-F]{64})").Groups[1].Value }
    $r | ConvertTo-Json -Compress
    return
}
# H13 (rework de pruebas, 24-oct-2026): el APK de ESTA compilacion, en su ruta exacta (configuracion + RID + nombre del paquete). Antes se cogia "el mas reciente de la carpeta", y bin\Release\net10.0-android\
# guarda a la vez android-arm64 y android-x64 de compilaciones anteriores: un APK residual de otro RID o de otra compilacion podia entregarse. Resolver-ApkExacto (lib-gate.ps1) falla si el esperado no esta.
$apk = if ($ArtifactsPath) { Resolver-ApkExacto (Join-Path $ArtifactsPath 'bin\Pokekeep.Mobile') $Rid $Configuracion -ConArtifactsPath } else { Resolver-ApkExacto (Join-Path $repo 'android\Pokekeep.Mobile\bin') $Rid $Configuracion }
# -r no sustituye RuntimeIdentifiers del csproj: el csproj lo toma de PokekeepRid (X26; nunca -p:RuntimeIdentifiers, que cambia la huella del Core).
# Esta guarda impide volver a entregar un APK con arquitecturas no solicitadas.
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($apk.FullName)
try {
    $abis = @($zip.Entries | ForEach-Object { if ($_.FullName -match '^lib/([^/]+)/') { $Matches[1] } } | Sort-Object -Unique)
    $esperada = if ($Rid -eq 'android-arm64') { 'arm64-v8a' } else { 'x86_64' }
    if ($abis.Count -ne 1 -or $abis[0] -ne $esperada) { throw "APK con ABI incorrecta: solicitada $Rid ($esperada); encontradas $($abis -join ', ')." }
} finally { $zip.Dispose() }
# La identidad embebida del motor tiene que ser la de los datos de Android (datos\android\identidad.json, construir-bd-android.ps1). Si no, el APK sale pero el motor
# rechaza todo con "El motor no coincide con esta versión" (pasó en uso5). Se comprueba aquí, antes de entregar nada.
if (-not $SoloIdentidad) {
    $idDatos = Join-Path $repo 'datos\android\identidad.json'
    $idGen = Get-ChildItem $motorObj -Recurse -Filter 'Identidad.g.cs' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not (Test-Path $idDatos)) { throw "Falta ${idDatos}: no se puede comprobar que el motor del APK sea el de los datos." }
    if (-not $idGen) { throw 'No se encontro Identidad.g.cs del motor tras compilar.' }
    $esperado = Get-Content $idDatos -Raw -Encoding UTF8 | ConvertFrom-Json
    $texto = Get-Content $idGen.FullName -Raw
    foreach ($par in @(@('Core', 'CoreSha256'), @('Engine', 'EngineSha256'), @('Adapter', 'AdapterSha256'), @('Alm', 'AlmSha256'))) {
        $m = [regex]::Match($texto, "Pokekeep\.$($par[1])"", ""([0-9A-F]{64})""")
        if (-not $m.Success -or $m.Groups[1].Value -ne $esperado.($par[0])) { throw "APK con motor distinto de los datos: $($par[0]) del APK $($m.Groups[1].Value.Substring(0, [Math]::Min(12, $m.Groups[1].Value.Length))), de los datos $($esperado.($par[0]).Substring(0, 12)). Recompila sin propiedades globales que cambien el Core o reconstruye los datos (construir-bd-android.ps1)." }
    }
}
Write-Host "APK: $($apk.FullName) ($([math]::Round($apk.Length/1MB,1)) MB)"
# X65/F13: ningún APK de producto arm64 Release se entrega sin auditar el
# artefacto firmado. Los APK de demostración/instrumentación no son producto.
if ($Rid -eq 'android-arm64' -and $Configuracion -eq 'Release' -and -not $Demo -and -not $Pruebas) {
    # La auditoria depende de KeepQA y de herramientas del autor: la exportacion publica no la lleva y el APK se compila sin ella.
    if (Test-Path (Join-Path $PSScriptRoot 'auditar-apk.ps1')) { & (Join-Path $PSScriptRoot 'auditar-apk.ps1') -Apk $apk.FullName -Sdk $script:AndroidSdkDirectory -Jdk $script:JavaSdkDirectory }
    else { Write-Host 'Sin auditar-apk.ps1 en esta copia: el APK no se audita (gate F13 solo en el repositorio de trabajo).' -ForegroundColor Yellow }
}
if ($Salida) { New-Item -ItemType Directory -Force (Split-Path $Salida) | Out-Null; Copy-Item $apk.FullName $Salida -Force; Write-Host "Copiado a $Salida" }
