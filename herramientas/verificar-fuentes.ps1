# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Pokekeep contributors
# Comprueba que cada carpeta de fuentes\ está EXACTAMENTE en el commit de fuentes.lock.json. Código de salida 1 si no.
. (Join-Path $PSScriptRoot 'entorno.ps1')
$lock = Get-Content (Join-Path $RepoRaiz 'fuentes.lock.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$fallos = 0
foreach ($f in $lock.fuentes) {
    $ruta = Join-Path $Raiz "fuentes\$($f.carpeta)"
    $head = (& $Git -c core.longpaths=true -C $ruta rev-parse HEAD 2>$null)
    $ok = ($head -eq $f.commit)
    if (-not $ok) { $fallos++ }
    '{0,-22} {1} {2}' -f $f.carpeta, $(if ($ok) { 'OK    ' } else { 'FALLO ' }), $head
}
if ($fallos) { Write-Error "$fallos fuente(s) fuera del commit fijado."; exit 1 }
Write-Host 'Todas las fuentes coinciden con fuentes.lock.json.'
