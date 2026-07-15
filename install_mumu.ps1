<#
.SYNOPSIS
    Instala y lanza File Manager en el emulador MuMu Player (constitucion 9 y 12).

.DESCRIPTION
    Resuelve adb y el puerto de MuMu, instala el APK y opcionalmente lo lanza.
    MuMu no expone MANAGE_EXTERNAL_STORAGE en el dialogo de permisos normal, asi que
    -GrantPermissions lo concede por appops para poder probar sin pasar por los ajustes.

.EXAMPLE
    ./install_mumu.ps1 -BuildFirst -Launch -GrantPermissions
#>
[CmdletBinding()]
param(
    [string]$ApkPath,
    [string]$AdbPath,
    [string]$DeviceSerial,
    [string]$PackageName,
    [string]$Configuration = "Debug",
    [string]$Framework = "net9.0-android36.0",
    [switch]$BuildFirst,
    [switch]$Launch,
    [switch]$GrantPermissions
)

$ErrorActionPreference = 'Stop'

$ProjectRoot = $PSScriptRoot
$ProjectPath = Join-Path $ProjectRoot 'FileManager.csproj'

$DefaultAdbPaths = @(
    'C:\Program Files\Netease\MuMuPlayer\nx_main\adb.exe',
    'C:\Program Files\Netease\MuMuPlayer\nx_device\12.0\shell\adb.exe',
    'C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe',
    (Join-Path $env:LOCALAPPDATA 'Android\Sdk\platform-tools\adb.exe')
)

# MuMu expone adb en un puerto distinto segun version e instancia.
$MumuPorts = @('127.0.0.1:16384', '127.0.0.1:7555', '127.0.0.1:16416', '127.0.0.1:5555')

function Exit-WithError {
    param([Parameter(Mandatory = $true)][string]$Message)
    Write-Host "ERROR: $Message" -ForegroundColor Red
    exit 1
}

function Resolve-AdbPath {
    if (-not [string]::IsNullOrWhiteSpace($AdbPath)) {
        if (Test-Path -LiteralPath $AdbPath) { return (Resolve-Path -LiteralPath $AdbPath).Path }
        Exit-WithError "No existe adb.exe en: $AdbPath"
    }

    foreach ($candidate in $DefaultAdbPaths) {
        if (-not [string]::IsNullOrWhiteSpace($candidate) -and (Test-Path -LiteralPath $candidate)) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    $command = Get-Command adb -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    Exit-WithError 'No se encontro adb.exe. Pasa la ruta con -AdbPath.'
}

function Get-AdbDevices {
    $raw = & $script:AdbPath devices
    if ($LASTEXITCODE -ne 0) { Exit-WithError 'No se pudo listar dispositivos ADB.' }

    return $raw | Where-Object { $_ -match '^\S+\s+device$' } | ForEach-Object { ($_ -split '\s+')[0] }
}

function Resolve-DeviceSerial {
    if (-not [string]::IsNullOrWhiteSpace($DeviceSerial)) { return $DeviceSerial }

    $devices = @(Get-AdbDevices)
    foreach ($port in $MumuPorts) {
        if ($devices -contains $port) { return $port }
    }

    foreach ($port in $MumuPorts) {
        Write-Host "Intentando conectar a MuMu en $port..."
        & $script:AdbPath connect $port | Out-Host
    }

    $devices = @(Get-AdbDevices)
    foreach ($port in $MumuPorts) {
        if ($devices -contains $port) { return $port }
    }

    if ($devices.Count -eq 1) { return $devices[0] }

    if ($devices.Count -gt 1) {
        Write-Host 'Dispositivos detectados:'
        $devices | ForEach-Object { Write-Host "  $_" }
        Exit-WithError 'Hay varios dispositivos ADB. Usa -DeviceSerial para elegir MuMu.'
    }

    Exit-WithError 'No se detecto MuMu por ADB. Abre MuMu Player y vuelve a ejecutar el script.'
}

function Get-ProjectValue {
    param(
        [Parameter(Mandatory = $true)][xml]$ProjectXml,
        [Parameter(Mandatory = $true)][string]$Name
    )

    foreach ($node in $ProjectXml.SelectNodes("//*[local-name()='$Name']")) {
        if (-not [string]::IsNullOrWhiteSpace($node.InnerText)) { return $node.InnerText.Trim() }
    }

    return $null
}

if (-not (Test-Path -LiteralPath $ProjectPath)) {
    Exit-WithError "No existe el proyecto: $ProjectPath"
}

$projectXml = [xml](Get-Content -LiteralPath $ProjectPath -Raw)
if ([string]::IsNullOrWhiteSpace($PackageName)) {
    $PackageName = Get-ProjectValue $projectXml 'ApplicationId'
}
if ([string]::IsNullOrWhiteSpace($PackageName)) {
    Exit-WithError 'No se pudo resolver ApplicationId desde el csproj.'
}

$script:AdbPath = Resolve-AdbPath

Write-Host '========================================'
Write-Host '   File Manager - Instalar en MuMu'
Write-Host '========================================'
Write-Host "ADB: $script:AdbPath"
Write-Host "Package: $PackageName"

if ($BuildFirst) {
    Write-Host "`nCompilando APK ($Configuration)..." -ForegroundColor Cyan
    dotnet build -c $Configuration -f $Framework
    if ($LASTEXITCODE -ne 0) { Exit-WithError 'Fallo el build del APK.' }
}

if ([string]::IsNullOrWhiteSpace($ApkPath)) {
    $latestApk = Get-ChildItem -LiteralPath (Join-Path $ProjectRoot 'bin') -Filter '*-Signed.apk' -Recurse -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1

    if (-not $latestApk) {
        Exit-WithError 'No se encontro ningun *-Signed.apk. Ejecuta con -BuildFirst.'
    }
    $ApkPath = $latestApk.FullName
}

if (-not (Test-Path -LiteralPath $ApkPath)) { Exit-WithError "No existe el APK: $ApkPath" }
$ApkPath = (Resolve-Path -LiteralPath $ApkPath).Path

$DeviceSerial = Resolve-DeviceSerial
Write-Host "Dispositivo MuMu: $DeviceSerial"
Write-Host "APK: $ApkPath`n"

# -d permite instalar sobre una version con versionCode mayor durante las pruebas.
& $script:AdbPath -s $DeviceSerial install -r -d $ApkPath | Out-Host
if ($LASTEXITCODE -ne 0) { Exit-WithError 'Fallo la instalacion del APK en MuMu.' }

if ($GrantPermissions) {
    Write-Host "`nConcediendo acceso a todos los ficheros para pruebas..."
    # MANAGE_EXTERNAL_STORAGE no se concede con 'pm grant': es un appop, no un permiso runtime.
    & $script:AdbPath -s $DeviceSerial shell appops set $PackageName MANAGE_EXTERNAL_STORAGE allow | Out-Host
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'AVISO: no se pudo conceder por appops. Concede el acceso desde la app.' -ForegroundColor Yellow
    }
}

if ($Launch) {
    Write-Host "`nLanzando app..."
    & $script:AdbPath -s $DeviceSerial shell monkey -p $PackageName -c android.intent.category.LAUNCHER 1 | Out-Null
    if ($LASTEXITCODE -ne 0) { Exit-WithError 'La app se instalo, pero no se pudo lanzar.' }
}

Write-Host "`nInstalacion completada en MuMu." -ForegroundColor Green
