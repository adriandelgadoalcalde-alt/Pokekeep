# Comprueba que ningún fichero del paquete ha cambiado respecto a Pokekeep\manifiesto.json.
# Uso: clic derecho > "Ejecutar con PowerShell" (o: powershell -ExecutionPolicy Bypass -File .\Verificar-paquete.ps1)
$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $MyInvocation.MyCommand.Path
$ruta = Join-Path $raiz 'Pokekeep\manifiesto.json'
if (-not (Test-Path $ruta)) { Write-Host 'No encuentro Pokekeep\manifiesto.json.' -ForegroundColor Red; exit 2 }
$m = Get-Content -Raw -Encoding UTF8 $ruta | ConvertFrom-Json
Write-Host ("{0} {1}: comprobando {2} ficheros..." -f $m.producto, $m.version, $m.archivos.Count)
$problemas = 0
foreach ($f in $m.archivos) {
    $p = Join-Path $raiz ($f.ruta -replace '/', '\')
    if (-not (Test-Path -LiteralPath $p)) { Write-Host "FALTA   $($f.ruta)" -ForegroundColor Red; $problemas++; continue }
    if ((Get-Item -LiteralPath $p).Length -ne $f.bytes) { Write-Host "TAMAÑO  $($f.ruta)" -ForegroundColor Red; $problemas++; continue }
    if ((Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash -ne $f.sha256) { Write-Host "SHA-256 $($f.ruta)" -ForegroundColor Red; $problemas++ }
}
$cores = @(Get-ChildItem -Path $raiz -Recurse -File -Filter 'PKHeX.Core.dll')
if ($cores.Count -ne 1) { Write-Host "Debe haber un solo PKHeX.Core.dll y hay $($cores.Count)." -ForegroundColor Red; $problemas++ }
if ($problemas -eq 0) { Write-Host 'Paquete íntegro: todos los ficheros coinciden.' -ForegroundColor Green; exit 0 }
Write-Host "$problemas problema(s). Vuelve a descomprimir el paquete original." -ForegroundColor Red
exit 1
