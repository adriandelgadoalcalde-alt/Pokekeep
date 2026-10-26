# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Pokekeep contributors
# Entorno reproducible de Pokekeep. Uso (dot-sourcing):  . .\herramientas\entorno.ps1
# Todas las cachés viven dentro de la raíz del proyecto; nada se instala fuera.
$script:RepoRaiz = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
# Desde el traslado del 05-oct-2026 (ADR 0039) el repo ES la raíz del proyecto: fuentes\, compilacion\, saves-prueba\ y las cachés viven dentro (ignoradas por git).
$script:Raiz = $RepoRaiz
$script:Entrega = Join-Path $Raiz 'entrega-codex'   # documentos y datos de entrada de la entrega inicial (no versionados)
$env:NUGET_PACKAGES = Join-Path $Raiz 'herramientas\nuget'
$env:DOTNET_CLI_HOME = Join-Path $Raiz 'herramientas\perfil-dotnet'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$script:Git = 'C:\Program Files\Git\cmd\git.exe'          # de RUTAS-Y-HERRAMIENTAS.json
$script:Dotnet = 'C:\Program Files\dotnet\dotnet.exe'      # de RUTAS-Y-HERRAMIENTAS.json
# Exportacion publica: fuera de la maquina del autor las rutas por defecto pueden no existir; se busca la herramienta en el PATH (ruta absoluta resuelta, nunca por nombre a secas).
foreach ($par in @(@('Git', 'git.exe'), @('Dotnet', 'dotnet.exe'))) {
    if (-not (Test-Path -LiteralPath (Get-Variable -Scope Script -Name $par[0]).Value)) {
        $c = Get-Command $par[1] -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($c) { Set-Variable -Scope Script -Name $par[0] -Value $c.Source }
    }
}
# Commit fijado del Core: SourceRevisionId fijo hace el PKHeX.Core.dll reproducible (si no, lleva la hora de compilación).
$script:CoreRev = 'bfe89c74a3f5'
# Hito 6: herramientas de empaquetado (rutas absolutas de RUTAS-Y-HERRAMIENTAS.json y de la carpeta de herramientas portables del usuario; nada personal en el repo).
$rutasJson = Join-Path $Entrega 'RUTAS-Y-HERRAMIENTAS.json'
$script:Python = if (Test-Path -LiteralPath $rutasJson) { (Get-Content $rutasJson -Raw | ConvertFrom-Json).tools.Python.path }
                 elseif ($env:POKEKEEP_PYTHON) { $env:POKEKEEP_PYTHON }
                 else { (Get-Command python.exe -ErrorAction SilentlyContinue | Select-Object -First 1).Source }   # solo lo usan las herramientas de auditoria y de cache; la exportacion publica no lo necesita
$script:SieteZip = Join-Path $env:USERPROFILE 'Downloads\Keep\dev-tools\7-Zip\7z.exe'
if (-not (Test-Path -LiteralPath $script:SieteZip)) {
    $script:SieteZip = if ($env:POKEKEEP_7ZIP) { $env:POKEKEEP_7ZIP } else { (Get-Command 7z.exe -ErrorAction SilentlyContinue | Select-Object -First 1).Source }   # solo para -Zip de construir-portable.ps1
}
