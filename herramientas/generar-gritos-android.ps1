# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Pokekeep contributors
<#
.SYNOPSIS
  Genera el paquete de gritos de serie de Android (X11): gritos.pkpack + manifest.json.
.DESCRIPTION
  Descarga (con caché en compilacion\x11\cache-gritos, ignorada por git) los .ogg de PokeAPI/cries y pokemon.csv / pokemon_forms.csv de PokeAPI/pokeapi, ambos en la revisión FIJADA que lleva el
  generador (Fuente.cs), traduce cada id de PokeAPI a (especie, forma) de PKHeX y empaqueta los .ogg ORIGINALES, sin recodificar, en un paquete con el mismo formato que los sprites.
  Escribe también docs\android\gritos-procedencia.tsv (clave, id, SHA-256, revisión, URL de cada sonido) y docs\android\gritos-sin-forma-propia.tsv (formas de PKHeX sin grito propio en PokeAPI).
  Salida por defecto: datos\android\gritos\ (ignorada por git, regenerable y determinista; el APK la incluye desde ahí). Dos ejecuciones con la misma caché dan bytes idénticos.
  Con -Verificar no escribe nada: vuelve a leer la salida y comprueba SHA-256 y que cada recurso es un Ogg con la huella de la procedencia.
  Con -Formas imprime el informe de la traducción PKHeX -> PokeAPI (formas sin asignar) para revisar formas-manuales.tsv.
  Con -SinDescargas solo usa la caché (red nunca en el producto: esto es una herramienta de construcción, no el APK).
  Con -ArtifactsPath la compilacion y la restauracion escriben fuera de las fuentes de terceros.
.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File herramientas\generar-gritos-android.ps1
  powershell -NoProfile -ExecutionPolicy Bypass -File herramientas\generar-gritos-android.ps1 -Verificar
#>
param([string]$Salida, [string]$Cache, [string]$PikachuUltrasol, [string]$Origen, [switch]$Verificar, [switch]$Formas, [switch]$SinDescargas, [string]$ArtifactsPath,
    # Compilacion desde el codigo publico: el grito de Pikachu del paquete de serie sale del audio privado de un juego del autor (C15), que no se publica.
    # Con -SinPikachuUltrasol el grito de Pikachu es el de PokeAPI, como el de las demas especies, y las tablas de procedencia se escriben en compilacion\android\ (docs\android\ no se toca).
    [switch]$SinPikachuUltrasol)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'entorno-android.ps1')
if (-not $script:AndroidListo) { throw 'Entorno Android incompleto.' }
$repo = $script:RepoRaiz
$nombreCopia = Split-Path $repo -Leaf
$corta = Join-Path $env:TEMP $(if ($nombreCopia -eq 'Pokekeep') { 'pkk-repo' } else { 'pkk-' + ($nombreCopia -replace '^Pokekeep-', '') })
if (-not (Test-Path $corta)) { New-Item -ItemType Junction -Path $corta -Target $repo | Out-Null }
if ((Get-Item $corta).Target -and ((Get-Item $corta).Target | Select-Object -First 1).TrimEnd([char]92) -ne $repo.TrimEnd([char]92)) { throw "La union $corta apunta a otra copia, no a $repo. Quitala (es solo un atajo de ruta) y vuelve a generar." }
if (-not $Salida) { $Salida = Join-Path $repo 'datos\android\gritos' }
if (-not $Cache) { $Cache = Join-Path $repo 'compilacion\x11\cache-gritos' }
$proc = Join-Path $repo 'docs\android\gritos-procedencia.tsv'
$sin = Join-Path $repo 'docs\android\gritos-sin-forma-propia.tsv'
$proyecto = Join-Path $corta 'android\Pokekeep.Gritos.Build\Pokekeep.Gritos.Build.csproj'
if ($SinPikachuUltrasol) {
    New-Item -ItemType Directory -Force (Join-Path $repo 'compilacion\android') | Out-Null
    $proc = Join-Path $repo 'compilacion\android\gritos-procedencia.tsv'
    $sin = Join-Path $repo 'compilacion\android\gritos-sin-forma-propia.tsv'
}
$modo = if ($Origen) { 'pikachu-ultrasol' } elseif ($Verificar) { 'verificar' } elseif ($Formas) { 'formas' } else { 'generar' }
$old = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
$aislado = if ($ArtifactsPath) { @('--artifacts-path', [IO.Path]::GetFullPath($ArtifactsPath)) } else { @() }
& $script:DotnetAndroid build $proyecto -c Release --nologo -v q -p:SourceRevisionId=bfe89c74a3f5 -p:UsedAvaloniaProducts= @aislado
$codigo = $LASTEXITCODE
if ($codigo -eq 0) {
    $bin = if ($ArtifactsPath) { Join-Path $ArtifactsPath 'bin\Pokekeep.Gritos.Build\release' } else { Join-Path $repo 'android\Pokekeep.Gritos.Build\bin\Release' }
    $dll = Get-ChildItem $bin -Recurse -Filter 'Pokekeep.Gritos.Build.dll' | Select-Object -First 1
    $a = @($dll.FullName, $modo)
    if ($modo -eq 'pikachu-ultrasol') { $a += @('--origen', $Origen, '--salida', $Salida, '--procedencia', $proc, '--pikachu-ultrasol', $PikachuUltrasol) }
    elseif ($modo -eq 'verificar') { $a += @('--salida', $Salida, '--procedencia', $proc) }
    else {
        $a += @('--cache', $Cache)
        if ($modo -eq 'generar') {
            $a += @('--salida', $Salida, '--procedencia', $proc, '--sin-grito', $sin)
            if ($SinPikachuUltrasol) { $a += '--sin-pikachu-ultrasol' }
            elseif ($PikachuUltrasol) { $a += @('--pikachu-ultrasol', $PikachuUltrasol) }
            else { throw 'Falta -PikachuUltrasol (WAV del autor) o -SinPikachuUltrasol.' }
        }
        if ($SinDescargas) { $a += '--sin-descargas' }
    }
    & $script:DotnetAndroid @a
    $codigo = $LASTEXITCODE
}
$ErrorActionPreference = $old
if ($codigo -ne 0) { throw "El generador de gritos fallo (codigo $codigo)." }
