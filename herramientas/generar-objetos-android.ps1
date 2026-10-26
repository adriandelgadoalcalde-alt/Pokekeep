# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Pokekeep contributors
<#
.SYNOPSIS
  Genera el paquete de objetos adicionales de Android (X14): objetos.pkpack + manifest.json, y el informe de cobertura de iconos por juego.
.DESCRIPTION
  Las MT/MO/DR con el icono de su TIPO (el del movimiento que enseña esa máquina en ese juego, de las tablas de PKHeX.Core) y los objetos que PKHeX no dibuja, de PokeAPI/sprites en la revisión FIJADA que lleva el
  generador (android\Pokekeep.Objetos.Build\Fuente.cs), con items.csv / item_names.csv de PokeAPI/pokeapi (también fijados) para cruzar nombres. PKHeX va siempre primero: solo entra lo que PKHeX no dibuja,
  salvo las máquinas, donde el icono por tipo gana al genérico. Empaqueta los PNG ORIGINALES, sin recodificar, en el mismo formato que los sprites.
  X27: en Esmeralda y FR/LG gana el arte de pret (id nativo, gráfico y paleta del juego), revisiones y hashes fijados en pret-gen3.tsv. Solo sustituye PLTE/CRC: índices, transparencia y dimensiones originales.
  Descarga con caché en compilacion\x14\cache-objetos (ignorada por git). Salida por defecto: compilacion\android\objetos\ (ignorada por git, regenerable y determinista: dos ejecuciones con la misma caché dan
  bytes idénticos) y una copia en datos\android\objetos\ (la carpeta compartida entre copias de trabajo; el .csproj de la app la busca primero ahí, igual que los gritos).
  Escribe además docs\android\sprites-objetos-procedencia.tsv (clave, identificador, SHA-256, revisión y URL de cada imagen) y, con la cobertura, docs\android\sprites-objetos-cobertura.tsv y
  docs\android\sprites-objetos-cobertura-resumen.tsv (por juego: qué objetos no tenían su sprite de PKHeX, antes y después, y el motivo de los que siguen sin arte).
  Con -Verificar no escribe nada: vuelve a leer la salida y comprueba SHA-256 del paquete, que cada recurso es un PNG decodificable y que su huella es la de la procedencia.
  Con -Mapa regenera android\Pokekeep.Sprites\ObjetosAntiguosTabla.cs (equivalencias de los objetos de Gen. 1-3 sin id moderno, por nombre) y sale.
  Con -SinDescargas solo usa la caché (la red nunca se usa en el producto: esto es una herramienta de construcción, no el APK).
.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File herramientas\generar-objetos-android.ps1
  powershell -NoProfile -ExecutionPolicy Bypass -File herramientas\generar-objetos-android.ps1 -Verificar
#>
# -SoloPaquete: genera y verifica el paquete, sin los informes de cobertura y matriz (que reescriben docs\android\ y piden las partidas publicas de saves-prueba; la exportacion publica no los lleva).
param([string]$Salida, [string]$Cache, [switch]$Verificar, [switch]$Mapa, [switch]$SinDescargas, [switch]$SinCopiaCompartida, [switch]$SoloPaquete,
    # Compilacion desde el codigo publico: sin las partidas publicas de saves-prueba, Colosseum y Leyendas: Arceus (cuya mochila solo se lee de una partida real) se omiten con un aviso,
    # y la tabla de procedencia se escribe en compilacion\android\ (docs\android\ no se toca).
    [switch]$SinPartidas)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'entorno-android.ps1')
if (-not $script:AndroidListo) { throw 'Entorno Android incompleto.' }
$repo = $script:RepoRaiz
$nombreCopia = Split-Path $repo -Leaf
$corta = Join-Path $env:TEMP $(if ($nombreCopia -eq 'Pokekeep') { 'pkk-repo' } else { 'pkk-' + ($nombreCopia -replace '^Pokekeep-', '') })
if (-not (Test-Path $corta)) { New-Item -ItemType Junction -Path $corta -Target $repo | Out-Null }
if ((Get-Item $corta).Target -and ((Get-Item $corta).Target | Select-Object -First 1).TrimEnd([char]92) -ne $repo.TrimEnd([char]92)) { throw "La union $corta apunta a otra copia, no a $repo. Quitala (es solo un atajo de ruta) y vuelve a generar." }
if (-not $Salida) { $Salida = Join-Path $repo 'compilacion\android\objetos' }
if (-not $Cache) { $Cache = Join-Path $repo 'compilacion\x14\cache-objetos' }
$sprites = Join-Path $repo 'datos\android\sprites'
$saves = Join-Path $repo 'saves-prueba\RoCs-PC'
$proc = Join-Path $repo 'docs\android\sprites-objetos-procedencia.tsv'
$detalle = Join-Path $repo 'docs\android\sprites-objetos-cobertura.tsv'
$resumen = Join-Path $repo 'docs\android\sprites-objetos-cobertura-resumen.tsv'
$sinResolver = Join-Path $repo 'compilacion\x14\objetos-sin-resolver.tsv'
$clavesX14 = Join-Path $repo 'docs\android\sprites-objetos-claves-x14.txt'
$matriz = Join-Path $repo 'docs\android\sprites-objetos-matriz.tsv'
$matrizResumen = Join-Path $repo 'docs\android\sprites-objetos-matriz-resumen.tsv'
$tabla = Join-Path $repo 'android\Pokekeep.Sprites\ObjetosAntiguosTabla.cs'
$proyecto = Join-Path $corta 'android\Pokekeep.Objetos.Build\Pokekeep.Objetos.Build.csproj'
if (-not (Test-Path (Join-Path $sprites 'manifest.json'))) { throw "Faltan los sprites de PKHeX en $sprites (herramientas\generar-sprites-android.ps1)." }
New-Item -ItemType Directory -Force (Split-Path $sinResolver) | Out-Null
if ($SinPartidas) {
    New-Item -ItemType Directory -Force (Join-Path $repo 'compilacion\android') | Out-Null
    $proc = Join-Path $repo 'compilacion\android\sprites-objetos-procedencia.tsv'
}
$old = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
& $script:DotnetAndroid build $proyecto -c Release -m:2 -nr:false --nologo -v q -p:SourceRevisionId=bfe89c74a3f5 -p:UsedAvaloniaProducts=
$codigo = $LASTEXITCODE
if ($codigo -eq 0) {
    $dll = (Get-ChildItem (Join-Path $repo 'android\Pokekeep.Objetos.Build\bin\Release') -Recurse -Filter 'Pokekeep.Objetos.Build.dll' | Select-Object -First 1).FullName
    if ($Mapa) {
        & $script:DotnetAndroid $dll mapa-antiguos --salida $tabla --sin-resolver (Join-Path $repo 'compilacion\x14\antiguos-sin-igualar.tsv')
        $codigo = $LASTEXITCODE
    } elseif ($Verificar) {
        & $script:DotnetAndroid $dll verificar --salida $Salida --procedencia $proc
        $codigo = $LASTEXITCODE
    } else {
        $a = @($dll, 'generar', '--cache', $Cache, '--sprites', $sprites, '--saves', $saves, '--salida', $Salida, '--procedencia', $proc, '--sin-resolver', $sinResolver)
        if ($SinDescargas) { $a += '--sin-descargas' }
        if ($SinPartidas) { $a += '--sin-partidas' }
        & $script:DotnetAndroid @a
        $codigo = $LASTEXITCODE
        if ($codigo -eq 0) {
            & $script:DotnetAndroid $dll verificar --salida $Salida --procedencia $proc
            $codigo = $LASTEXITCODE
        }
        if ($codigo -eq 0 -and -not $SoloPaquete) {
            & $script:DotnetAndroid $dll cobertura --sprites $sprites --objetos $Salida --saves $saves --sin-resolver $sinResolver --detalle $detalle --resumen $resumen
            $codigo = $LASTEXITCODE
        }
        if ($codigo -eq 0 -and -not $SoloPaquete) {
            & $script:DotnetAndroid $dll matriz --sprites $sprites --objetos $Salida --saves $saves --sin-resolver $sinResolver --procedencia $proc --claves-x14 $clavesX14 --detalle $matriz --resumen $matrizResumen
            $codigo = $LASTEXITCODE
        }
        if ($codigo -eq 0 -and -not $SinCopiaCompartida) {
            $compartida = Join-Path $repo 'datos\android\objetos'
            New-Item -ItemType Directory -Force $compartida | Out-Null
            Copy-Item (Join-Path $Salida 'manifest.json'), (Join-Path $Salida 'objetos.pkpack') $compartida -Force
            Write-Host "Copiado a $compartida"
        }
    }
}
$ErrorActionPreference = $old
if ($codigo -ne 0) { throw "El generador de objetos fallo (codigo $codigo)." }
