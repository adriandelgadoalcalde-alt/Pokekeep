# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Pokekeep contributors
<#
.SYNOPSIS
  Entorno de compilacion para Android (Fase 1, ADR 0054): .NET SDK AISLADO con la carga de trabajo android, Android SDK y JDK, todo dentro del repo e ignorado por git.
.DESCRIPTION
  Uso (dot-sourcing, como entorno.ps1):   . .\herramientas\entorno-android.ps1 [-Instalar] [-Maui]
  Sin -Instalar solo fija las variables (y falla con un mensaje claro si falta algo). Con -Instalar descarga e instala lo que falte, sin tocar nada fuera de la carpeta del repo
  (salvo la union corta de abajo): .NET SDK 10.0.401 en herramientas\dotnet-android\ (dotnet-install.ps1 -NoPath), la carga de trabajo `android` (con -Maui, tambien `maui-android`),
  el Android SDK en herramientas\android-sdk\ (cmdline-tools, platform-tools, build-tools;36.0.0, platforms;android-36) y la cache de NuGet propia en herramientas\nuget-android\.
  NO instala el emulador ni las imagenes de sistema: `sdkmanager` queda preparado para que F0/F2 instalen `emulator` y `system-images;android-<api>;google_apis;x86_64`.

  TRAMPA VERIFICADA (Fase 0, fw-spike): con rutas largas el empaquetado de Android falla con XARLP7000 (libZipSharpNative). Por eso todo se usa a traves de una UNION corta
  (%TEMP%\pkk-android -> <repo>\herramientas): las variables apuntan a la union, nunca a la ruta larga del repo.

  Variables que fija: DOTNET_ROOT, NUGET_PACKAGES, DOTNET_CLI_HOME, JAVA_HOME, ANDROID_SDK_ROOT/ANDROID_HOME, ANDROID_USER_HOME, AndroidSdkDirectory y JavaSdkDirectory (estas dos tambien como
  $script:AndroidSdkDirectory / $script:JavaSdkDirectory para pasarlas a MSBuild con -p:), y antepone dotnet-android, platform-tools y sdkmanager al PATH de ESTA sesion. Expone $script:DotnetAndroid.
  No modifica el entorno.ps1 del escritorio: el dotnet global (entorno.ps1) y el de Android son independientes.
#>
param([switch]$Instalar, [switch]$Maui)
$ErrorActionPreference = 'Stop'
$script:RepoRaiz = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$herramientas = Join-Path $script:RepoRaiz 'herramientas'

# --- Union corta (ruta corta VERIFICADA contra XARLP7000) ---
$script:AndroidCorta = Join-Path $env:TEMP 'pkk-android'
if (-not (Test-Path $script:AndroidCorta)) {
    New-Item -ItemType Junction -Path $script:AndroidCorta -Target $herramientas | Out-Null
} elseif ((Get-Item $script:AndroidCorta).Target -ne $herramientas -and (Get-Item $script:AndroidCorta).LinkType -eq 'Junction' -and
          -not (Test-Path (Join-Path $script:AndroidCorta 'dotnet-android\dotnet.exe'))) {
    # Union de otro clon del repo SIN entorno completo: se reapunta a este (es solo un atajo de ruta). Si ya da un entorno completo se deja:
    # las copias de trabajo de fases en paralelo (crear-copia-fase.ps1) enlazan dotnet-android, nuget-android y android-sdk al MISMO sitio,
    # y reapuntarla en mitad de la compilacion de la otra copia le romperia las rutas.
    (Get-Item $script:AndroidCorta).Delete()
    New-Item -ItemType Junction -Path $script:AndroidCorta -Target $herramientas | Out-Null
}
$k = $script:AndroidCorta
$script:DotnetAndroid = Join-Path $k 'dotnet-android\dotnet.exe'
$sdk = Join-Path $k 'android-sdk'
$jdk = if ($env:POKEKEEP_JDK) { $env:POKEKEEP_JDK } else { 'C:\Program Files\Java\jdk-21.0.12' }

$env:DOTNET_ROOT = Join-Path $k 'dotnet-android'
$env:NUGET_PACKAGES = Join-Path $k 'nuget-android'
$env:DOTNET_CLI_HOME = Join-Path $k 'perfil-dotnet-android'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'; $env:DOTNET_NOLOGO = '1'; $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:JAVA_HOME = $jdk
$env:ANDROID_SDK_ROOT = $sdk; $env:ANDROID_HOME = $sdk
$env:ANDROID_USER_HOME = Join-Path $k 'android-user'
$env:ANDROID_AVD_HOME = Join-Path $k 'android-user\avd'
$script:AndroidSdkDirectory = $sdk
$script:JavaSdkDirectory = $jdk
$sdkmanager = Join-Path $sdk 'cmdline-tools\latest\bin\sdkmanager.bat'
$env:Path = (@((Join-Path $k 'dotnet-android'), (Join-Path $sdk 'platform-tools'), (Join-Path $sdk 'cmdline-tools\latest\bin'), (Join-Path $jdk 'bin')) -join ';') + ';' + $env:Path

function Invoke-Descarga([string]$Url, [string]$Destino) {
    Write-Host "Descargando $Url" -ForegroundColor Cyan
    # curl.exe (incluido en Windows 10/11): Invoke-WebRequest de Windows PowerShell 5.1 con la barra de progreso se quedo parado en la descarga de 147 MB de cmdline-tools (verificado en la Fase 1).
    $curl = Get-Command curl.exe -ErrorAction SilentlyContinue
    if ($curl) {
        & $curl.Source -L --fail --silent --show-error --retry 3 --connect-timeout 30 -o $Destino $Url
        if ($LASTEXITCODE -ne 0) { throw "curl.exe no pudo descargar $Url (codigo $LASTEXITCODE)." }
    } else {
        $ProgressPreference = 'SilentlyContinue'
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $Url -OutFile $Destino -UseBasicParsing
    }
}

if ($Instalar) {
    if (-not (Test-Path (Join-Path $jdk 'bin\java.exe'))) { throw "Falta el JDK 21 en $jdk (o fija POKEKEEP_JDK)." }
    New-Item -ItemType Directory -Force $env:NUGET_PACKAGES, $env:DOTNET_CLI_HOME, $env:ANDROID_USER_HOME | Out-Null

    # 1. .NET SDK aislado (la version de global.json).
    $version = (Get-Content (Join-Path $script:RepoRaiz 'global.json') -Raw | ConvertFrom-Json).sdk.version
    if (-not (Test-Path $script:DotnetAndroid)) {
        $inst = Join-Path $k 'dotnet-android-instalador.ps1'
        Invoke-Descarga 'https://dot.net/v1/dotnet-install.ps1' $inst
        & powershell -NoProfile -ExecutionPolicy Bypass -File $inst -Version $version -InstallDir (Join-Path $k 'dotnet-android') -NoPath
        if (-not (Test-Path $script:DotnetAndroid)) { throw 'dotnet-install no dejo dotnet.exe.' }
    }
    Write-Host ".NET SDK: $(& $script:DotnetAndroid --version)" -ForegroundColor Green

    # 2. Cargas de trabajo.
    $cargas = (& $script:DotnetAndroid workload list) -join "`n"
    foreach ($w in @('android') + $(if ($Maui) { 'maui-android' } else { @() })) {
        if ($cargas -notmatch "(?m)^$w\s") {
            Write-Host "Instalando la carga de trabajo $w" -ForegroundColor Cyan
            & $script:DotnetAndroid workload install $w --skip-sign-check
            if ($LASTEXITCODE -ne 0) { throw "Fallo la carga de trabajo $w." }
        }
    }

    # 3. Android SDK: cmdline-tools (ultima version estable del repositorio de Google) y los paquetes minimos.
    if (-not (Test-Path $sdkmanager)) {
        $xml = Join-Path $k 'android-repository2.xml'
        Invoke-Descarga 'https://dl.google.com/android/repository/repository2-3.xml' $xml
        [xml]$repo = Get-Content $xml -Raw
        $pkg = $repo.'sdk-repository'.remotePackage | Where-Object { $_.path -eq 'cmdline-tools;latest' } | Select-Object -First 1
        $arch = $pkg.archives.archive | Where-Object { $_.'host-os' -eq 'windows' } | Select-Object -First 1
        if (-not $arch) { throw 'No encuentro cmdline-tools para Windows en el repositorio de Google.' }
        $zip = Join-Path $k 'cmdline-tools.zip'
        Invoke-Descarga ('https://dl.google.com/android/repository/' + $arch.complete.url) $zip
        $tmp = Join-Path $k 'cmdline-tools-tmp'
        if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
        Expand-Archive $zip -DestinationPath $tmp
        New-Item -ItemType Directory -Force (Join-Path $sdk 'cmdline-tools') | Out-Null
        Move-Item (Join-Path $tmp 'cmdline-tools') (Join-Path $sdk 'cmdline-tools\latest')
        Remove-Item $tmp, $zip, $xml -Recurse -Force
    }
    # Licencias (las del Android SDK; aceptarlas es necesario para instalar los paquetes) y paquetes del encargo: platform-tools, build-tools 36.0.0, platforms android-36.
    ('y' * 30 -split '') -join "`n" | & $sdkmanager --sdk_root=$sdk --licenses | Out-Null
    # sdkmanager es un .bat: los ';' de los nombres de paquete hay que entrecomillarlos para que cmd no los parta.
    & $sdkmanager "--sdk_root=$sdk" 'platform-tools' '"build-tools;36.0.0"' '"platforms;android-36"'
    if ($LASTEXITCODE -ne 0) { throw 'sdkmanager no pudo instalar los paquetes.' }
}

# --- Comprobacion (siempre) ---
$faltan = @()
if (-not (Test-Path $script:DotnetAndroid)) { $faltan += "dotnet-android ($script:DotnetAndroid)" }
if (-not (Test-Path (Join-Path $sdk 'platforms\android-36'))) { $faltan += 'Android SDK platforms;android-36' }
if (-not (Test-Path (Join-Path $sdk 'build-tools\36.0.0'))) { $faltan += 'Android SDK build-tools;36.0.0' }
if (-not (Test-Path (Join-Path $jdk 'bin\java.exe'))) { $faltan += "JDK 21 ($jdk)" }
if ($faltan.Count -gt 0) {
    Write-Warning ("Entorno Android incompleto: " + ($faltan -join '; ') + ". Ejecuta:  . .\herramientas\entorno-android.ps1 -Instalar")
    $script:AndroidListo = $false
} else {
    $script:AndroidListo = $true
}
