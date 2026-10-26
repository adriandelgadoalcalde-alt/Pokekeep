# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Pokekeep contributors
<#
.SYNOPSIS
  Descarga las fuentes de terceros de fuentes.lock.json (PKHeX, ALM, EventsGallery, PoGoEncTool) en fuentes\, cada una en su commit fijado.
  Downloads the third-party sources listed in fuentes.lock.json into fuentes\, each one at its pinned commit.
.DESCRIPTION
  Para cada entrada del lock: si fuentes\<carpeta> ya esta en el commit fijado no hace nada; si no existe, hace un clon superficial de ESE commit (git fetch --depth 1)
  y lo deja en HEAD desacoplado. Si la carpeta existe pero esta en otro commit, para con un error: no la toca (puede tener trabajo dentro).
  Despues, herramientas\verificar-fuentes.ps1 comprueba que todo coincide con el lock.
  -Solo limita la descarga a las carpetas indicadas.
.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File herramientas\obtener-fuentes.ps1
#>
param([string[]]$Solo = @())
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'entorno.ps1')
$lock = Get-Content (Join-Path $RepoRaiz 'fuentes.lock.json') -Raw -Encoding UTF8 | ConvertFrom-Json
New-Item -ItemType Directory -Force (Join-Path $Raiz 'fuentes') | Out-Null

# git escribe el progreso por stderr: con ErrorActionPreference=Stop, Windows PowerShell 5.1 lo tomaria por un error. Se mira el codigo de salida.
function Git-Ok([string[]]$Argumentos, [string]$Que) {
    $old = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    & $Git -c core.longpaths=true @Argumentos 2>&1 | ForEach-Object { Write-Host "    $_" }
    $codigo = $LASTEXITCODE
    $ErrorActionPreference = $old
    if ($codigo -ne 0) { throw "Fallo: $Que (git codigo $codigo)" }
}

foreach ($f in $lock.fuentes) {
    if ($Solo.Count -gt 0 -and $Solo -notcontains $f.carpeta) { continue }
    $destino = Join-Path $Raiz "fuentes\$($f.carpeta)"
    if (Test-Path -LiteralPath (Join-Path $destino '.git')) {
        $old = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
        $head = (& $Git -c core.longpaths=true -C $destino rev-parse HEAD 2>$null)
        $ErrorActionPreference = $old
        if ($head -eq $f.commit) { Write-Host ("{0,-22} ya esta en {1}" -f $f.carpeta, $f.commit.Substring(0, 12)); continue }
        throw "$destino existe pero esta en $head, no en $($f.commit). No se toca: revisalo y borra o corrige la carpeta tu mismo."
    }
    if ((Test-Path -LiteralPath $destino) -and @(Get-ChildItem -LiteralPath $destino -Force).Count -gt 0) { throw "$destino existe y no esta vacia ni es un repositorio de git. No se toca." }
    Write-Host ("{0,-22} descargando {1} @ {2}" -f $f.carpeta, $f.origen, $f.commit.Substring(0, 12)) -ForegroundColor Cyan
    New-Item -ItemType Directory -Force $destino | Out-Null
    Git-Ok @('-C', $destino, 'init', '-q') "git init de $($f.carpeta)"
    Git-Ok @('-C', $destino, 'remote', 'add', 'origin', $f.origen) "git remote add de $($f.carpeta)"
    Git-Ok @('-C', $destino, 'fetch', '--depth', '1', '-q', 'origin', $f.commit) "git fetch del commit fijado de $($f.carpeta)"
    Git-Ok @('-C', $destino, 'checkout', '-q', '--detach', 'FETCH_HEAD') "git checkout de $($f.carpeta)"
}
& (Join-Path $PSScriptRoot 'verificar-fuentes.ps1')
if ($LASTEXITCODE -ne 0) { throw 'verificar-fuentes.ps1 no da el visto bueno.' }
