# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Pokekeep contributors
# Funciones comunes de los gates de pruebas (gate-suites.ps1, android\herramientas\probar-visuales.ps1 y herramientas\compilar-apk.ps1).
# Se carga con dot-sourcing. Sin efectos al cargarse: solo define funciones. Su contrato lo demuestra herramientas\tests\probar-lib-gate.ps1.

function Nombre-Caso([string]$linea) { ($linea -replace '\s*\[(STARTING|FINISHED)\].*$', '').Trim() }
function Clase-Metodo([string]$caso) { $i = $caso.IndexOf('('); if ($i -ge 0) { $caso.Substring(0, $i) } else { $caso } }
function Clase-De([string]$cm) { $cm.Substring(0, $cm.LastIndexOf('.')) }

function Leer-Log([string]$ruta) {
    # Texto del log aunque otro proceso lo siga escribiendo.
    $fs = [IO.File]::Open($ruta, 'Open', 'Read', 'ReadWrite')
    try { $sr = New-Object IO.StreamReader($fs, [Text.Encoding]::UTF8); return $sr.ReadToEnd() } finally { $fs.Dispose() }
}

function Matar-Arbol($proc) { if ($proc -and -not $proc.HasExited) { & taskkill /PID $proc.Id /T /F 2>&1 | Out-Null } }

function Get-MemoriaLibreGB {
    # GlobalMemoryStatusEx funciona aunque WMI este restringido; WMI queda de respaldo. Devuelve -1 si no se puede observar.
    try {
        if (-not ('MemoriaGatePokekeep' -as [type])) {
            Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class MemoriaGatePokekeep {
    [StructLayout(LayoutKind.Sequential)] public struct Estado {
        public uint Length, Load;
        public ulong Total, Available, PageTotal, PageAvailable, VirtualTotal, VirtualAvailable, Extended;
    }
    [DllImport("kernel32.dll", SetLastError=true)] private static extern bool GlobalMemoryStatusEx(ref Estado estado);
    public static double LibreGB() {
        var estado = new Estado(); estado.Length = (uint)Marshal.SizeOf(estado);
        if (!GlobalMemoryStatusEx(ref estado)) throw new InvalidOperationException("Memoria libre no observable.");
        return estado.Available / 1073741824.0;
    }
}
'@
        }
        return [MemoriaGatePokekeep]::LibreGB()
    } catch {
        try { return (Get-CimInstance Win32_OperatingSystem -OperationTimeoutSec 10 -ErrorAction Stop).FreePhysicalMemory / 1MB } catch { return -1 }
    }
}

# Tabla de duraciones: TSV `clase<TAB>caso<TAB>segundos` (una fila por caso ejecutado). Devuelve una lista de objetos (los nombres de caso pueden repetirse: xUnit recorta los argumentos largos).
function Leer-TablaDuraciones([string]$ruta) {
    $filas = New-Object 'System.Collections.Generic.List[object]'
    if (-not $ruta -or -not (Test-Path -LiteralPath $ruta)) { return , $filas }
    $primera = $true
    foreach ($l in [IO.File]::ReadLines($ruta, [Text.Encoding]::UTF8)) {
        if ($primera) { $primera = $false; continue }
        $p = $l -split "`t"
        if ($p.Count -ge 3) { $filas.Add([pscustomobject]@{ Clase = $p[0]; Caso = $p[1]; Seg = [double]::Parse($p[2], [Globalization.CultureInfo]::InvariantCulture) }) }
    }
    return , $filas
}
function Escribir-TablaDuraciones([string]$ruta, $filas) {
    $ruta = [IO.Path]::GetFullPath($ruta)
    New-Item -ItemType Directory -Force (Split-Path $ruta) | Out-Null
    $sb = New-Object Text.StringBuilder
    [void]$sb.Append("clase`tcaso`tsegundos`r`n")
    foreach ($f in $filas) { [void]$sb.Append(("{0}`t{1}`t{2}`r`n" -f $f.Clase, $f.Caso, $f.Seg.ToString('0.###', [Globalization.CultureInfo]::InvariantCulture))) }
    [IO.File]::WriteAllText($ruta, $sb.ToString(), (New-Object Text.UTF8Encoding($false)))
}

# Reparto LPT: la unidad mas pesada al grupo menos cargado. $pesos: hashtable nombre -> peso. Devuelve @{ Grupos = lista de listas de nombres; Cargas = double[] }.
function Repartir-Lpt($pesos, [int]$n) {
    if ($n -lt 1) { throw 'Repartir-Lpt: n debe ser >= 1.' }
    $carga = New-Object 'double[]' $n
    $grupos = @(); for ($i = 0; $i -lt $n; $i++) { $grupos += , (New-Object Collections.ArrayList) }
    foreach ($c in ($pesos.Keys | Sort-Object { -[double]$pesos[$_] }, { $_ })) {
        $min = 0; for ($i = 1; $i -lt $n; $i++) { if ($carga[$i] -lt $carga[$min]) { $min = $i } }
        [void]$grupos[$min].Add($c); $carga[$min] += [double]$pesos[$c]
    }
    return @{ Grupos = $grupos; Cargas = $carga }
}

# H12: cada ejecucion escribe en SU carpeta <raiz>\<runId>; nada se borra de otras ejecuciones. El runId lleva marca de tiempo, proceso y 8 hex aleatorios:
# dos ejecuciones simultaneas o consecutivas no pueden coincidir. La carpeta se crea con CreateDirectory exclusivo (si existiera, se elige otro id).
function Nueva-CarpetaEjecucion([string]$Raiz, [string]$RunId = '') {
    $Raiz = [IO.Path]::GetFullPath($Raiz)
    New-Item -ItemType Directory -Force $Raiz | Out-Null
    for ($intento = 0; $intento -lt 10; $intento++) {
        $id = if ($RunId -and $intento -eq 0) { $RunId } else { '{0}-{1}-{2}' -f (Get-Date -Format 'yyyyMMdd-HHmmss'), $PID, ([Guid]::NewGuid().ToString('N').Substring(0, 8)) }
        $ruta = Join-Path $Raiz $id
        if (-not (Test-Path -LiteralPath $ruta)) {
            try { [void][IO.Directory]::CreateDirectory($ruta); return $ruta } catch { }
        } elseif ($RunId -and $intento -eq 0) {
            throw "La carpeta de ejecucion $ruta ya existe: no se reutiliza ni se borra (cada ejecucion tiene la suya)."
        }
    }
    throw "No se pudo crear una carpeta de ejecucion unica en $Raiz."
}
function Nueva-CarpetaPasada([string]$CarpetaEjecucion, [int]$Pasada) {
    $ruta = Join-Path $CarpetaEjecucion ("pasada-{0}" -f $Pasada)
    if (Test-Path -LiteralPath $ruta) { throw "La carpeta de pasada $ruta ya existe: no se sobrescribe." }
    [void][IO.Directory]::CreateDirectory($ruta)
    return $ruta
}

# H13: el APK de ESTA compilacion es el que sale en la ruta exacta (carpeta del RID y de la configuracion, nombre ApplicationId + '-Signed.apk'). Nunca "el mas reciente de la carpeta":
# bin\Release\net10.0-android\ guarda a la vez android-arm64 y android-x64 de compilaciones anteriores. Falla si el fichero esperado no esta.
function Resolver-ApkExacto([string]$Bin, [string]$Rid, [string]$Configuracion = 'Release', [string]$ApplicationId = 'com.incredibad.pokekeep', [string]$Tfm = 'net10.0-android', [switch]$ConArtifactsPath) {
    # Dentro de la carpeta exacta de la configuracion y el RID, primero el firmado y, solo si no esta, el sin firmar del mismo paquete.
    $nombres = @("$ApplicationId-Signed.apk", "$ApplicationId.apk")
    if ($ConArtifactsPath) {
        # --artifacts-path: <artifacts>\bin\Pokekeep.Mobile\<configuracion en minusculas>_<rid>\ (el RID va en el nombre de la carpeta; el APK puede estar a mas profundidad dentro de ella)
        $carpeta = Join-Path $Bin ($Configuracion.ToLowerInvariant() + '_' + $Rid)
        if (Test-Path -LiteralPath $carpeta -PathType Container) {
            foreach ($nombre in $nombres) {
                $dentro = @(Get-ChildItem -LiteralPath $carpeta -Recurse -Filter $nombre -File -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending)
                if ($dentro.Count -gt 0) { return $dentro[0] }
            }
        }
    } else {
        $carpeta = Join-Path $Bin (Join-Path $Configuracion (Join-Path $Tfm $Rid))
        foreach ($nombre in $nombres) {
            $ruta = Join-Path $carpeta $nombre
            if (Test-Path -LiteralPath $ruta -PathType Leaf) { return (Get-Item -LiteralPath $ruta) }
        }
    }
    throw ("No esta el APK de esta compilacion ({0}, {1}): se esperaba {2} dentro de {3}. No se entrega ningun otro APK de la carpeta." -f $Rid, $Configuracion, ($nombres -join ' o '), $carpeta)
}

# Cuadre de un listado contra lo ejecutado. Devuelve la lista de problemas (vacia = cuadra); nunca rebaja nada.
#   $MetodosListados: 'Clase.Metodo' del listado completo; $CasosEjecutados: nombres completos de los casos con [FINISHED];
#   $Esperado: total exigido (0 = no se exige); $Total: total que dicen los resumenes de los procesos.
function Comprobar-Cuadre([string[]]$MetodosListados, [string[]]$CasosEjecutados, [int]$Total, [int]$Esperado = 0) {
    $problemas = New-Object 'System.Collections.Generic.List[string]'
    $ejecutados = New-Object 'System.Collections.Generic.HashSet[string]'
    foreach ($c in $CasosEjecutados) { [void]$ejecutados.Add((Clase-Metodo $c)) }
    $faltan = @($MetodosListados | Where-Object { -not $ejecutados.Contains($_) })
    if ($faltan.Count -gt 0) { $problemas.Add(("metodos del listado que no se han ejecutado ({0}): {1}" -f $faltan.Count, (($faltan | Select-Object -First 5) -join '; '))) }
    if ($Total -ne $CasosEjecutados.Count) { $problemas.Add(("la suma de los resumenes ({0}) no coincide con los casos terminados ({1})" -f $Total, $CasosEjecutados.Count)) }
    if ($Esperado -gt 0 -and $Total -ne $Esperado) { $problemas.Add(("se esperaban {0} pruebas y se ejecutaron {1}" -f $Esperado, $Total)) }
    return , $problemas
}

# Los logs de un ejecutor de xUnit lanzado con `& exe ... *> log` desde un PowerShell de ventana oculta salen en UTF-16 con las letras no ASCII ya estropeadas: la consola decodifico los bytes UTF-8 del
# ejecutor como OEM 850 (la n con tilde sale como dos simbolos de dibujo de cajas). Como CP850 asigna un caracter distinto a cada byte, se deshace exactamente: caracteres -> bytes CP850 -> texto UTF-8.
# Solo para esos logs (los de Start-Process ya son UTF-8).
function Leer-LogOem([string]$ruta) {
    $texto = Leer-Log $ruta
    $cp850 = [Text.Encoding]::GetEncoding(850)
    $bytes = $cp850.GetBytes($texto)
    $reparado = (New-Object Text.UTF8Encoding($false, $true))
    try { return $reparado.GetString($bytes) } catch { return $texto }   # si no era UTF-8 estropeado, se deja tal cual
}

# Listado de una suite de xUnit v3: `exe -list full/json` leido como UTF-8 desde un fichero. Con `& exe | Out-String` la consola decodifica con OEM 850 (en un PowerShell oculto) y los metodos con
# n con tilde dejan de coincidir con los de los logs, que ya son UTF-8: el cuadre daria "no se han ejecutado" para pruebas que si se ejecutaron.
function Listar-Xunit([string]$Exe) {
    $tmp = [IO.Path]::Combine([IO.Path]::GetTempPath(), 'listado-' + [Guid]::NewGuid().ToString('N') + '.json')
    try {
        $p = Start-Process -FilePath $Exe -ArgumentList @('-list', 'full/json') -WorkingDirectory (Split-Path $Exe) -RedirectStandardOutput $tmp -RedirectStandardError ($tmp + '.err') -WindowStyle Hidden -PassThru
        $null = $p.Handle
        $p.WaitForExit()
        $json = if (Test-Path -LiteralPath $tmp) { [IO.File]::ReadAllText($tmp, (New-Object Text.UTF8Encoding($false))) } else { '' }
    } finally { Remove-Item -LiteralPath $tmp, ($tmp + '.err') -Force -ErrorAction SilentlyContinue }
    $ini = $json.IndexOf('[{')
    if ($ini -lt 0) { throw "El listado de $Exe (-list full/json) no devolvio nada." }
    return ($json.Substring($ini) | ConvertFrom-Json)
}
