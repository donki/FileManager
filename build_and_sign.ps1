<#
.SYNOPSIS
    Compila y firma File Manager para Android (constitucion 6 y 7).

.DESCRIPTION
    Genera un APK (Debug, para pruebas en dispositivo) y un AAB (Release, para Google Play),
    ambos firmados y reproducibles a partir del codigo fuente.

    La contrasena del keystore NUNCA se escribe en este fichero (constitucion 5). Se resuelve,
    en este orden:
      1. Parametro -StorePass.
      2. Variable de entorno ANDROID_KEYSTORE_PASSWORD.
      3. Fichero keystore.password.txt en la raiz (ignorado por git).
    Si ninguna fuente la aporta, el script falla en lugar de recurrir a un valor por defecto.

.EXAMPLE
    ./build_and_sign.ps1 -Clean

.EXAMPLE
    $env:ANDROID_KEYSTORE_PASSWORD = "..."
    ./build_and_sign.ps1 -SkipApk -NoPause
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$TargetFramework,
    [string]$DotnetPath,
    [string]$KeystorePath,
    [string]$KeyAlias = 'smsforwarder',
    [string]$StorePass,
    [string]$KeyPass,
    [string]$IntermediateOutputRoot,
    [switch]$Clean,
    [switch]$SkipApk,
    [switch]$SkipClean,
    [switch]$NoPause
)

$ErrorActionPreference = 'Stop'

$ProjectRoot = $PSScriptRoot
$ProjectPath = Join-Path $ProjectRoot 'FileManager.csproj'

function Pause-Script {
    if (-not $NoPause) {
        Read-Host 'Pulsa Enter para continuar' | Out-Null
    }
}

function Exit-WithError {
    param([Parameter(Mandatory = $true)][string]$Message)

    Write-Host "ERROR: $Message" -ForegroundColor Red
    Pause-Script
    exit 1
}

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory = $true)][string]$ErrorMessage,
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    # Los argumentos de firma llevan las contrasenas en claro: se redactan antes de mostrarlos
    # para que no acaben en la consola ni en un log de CI (constitucion 5).
    $shown = $Arguments | ForEach-Object {
        if ($_ -match '^(-p:Android(Signing(Key|Store)Pass))=') { "$($Matches[1])=***" } else { $_ }
    }
    Write-Host "> $FilePath $($shown -join ' ')"

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        Exit-WithError $ErrorMessage
    }
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

function Resolve-ToolPath {
    param(
        [string]$ConfiguredPath,
        [Parameter(Mandatory = $true)][string]$CommandName,
        [string]$FallbackPath
    )

    if (-not [string]::IsNullOrWhiteSpace($ConfiguredPath)) {
        if (Test-Path -LiteralPath $ConfiguredPath) { return (Resolve-Path -LiteralPath $ConfiguredPath).Path }
        Exit-WithError "No existe $CommandName en: $ConfiguredPath"
    }

    $command = Get-Command $CommandName -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    if (-not [string]::IsNullOrWhiteSpace($FallbackPath) -and (Test-Path -LiteralPath $FallbackPath)) {
        return $FallbackPath
    }

    Exit-WithError "No se encontro $CommandName. Pasalo con -DotnetPath."
}

function Get-LatestPackage {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Filter,
        [switch]$PreferPublishFolder
    )

    Get-ChildItem -LiteralPath $Root -Filter $Filter -Recurse -ErrorAction SilentlyContinue |
        Sort-Object @{ Expression = { if ($PreferPublishFolder -and $_.DirectoryName -like '*\publish') { 0 } else { 1 } } }, LastWriteTime -Descending |
        Select-Object -First 1
}

if (-not (Test-Path -LiteralPath $ProjectPath)) {
    Exit-WithError "No existe el proyecto: $ProjectPath"
}

$projectXml = [xml](Get-Content -LiteralPath $ProjectPath -Raw)
$packageName = Get-ProjectValue $projectXml 'ApplicationId'
if ([string]::IsNullOrWhiteSpace($packageName)) {
    Exit-WithError 'No se encontro ApplicationId en el csproj.'
}

# La version declarada en el csproj es la unica fuente de verdad (constitucion 6).
$displayVersion = Get-ProjectValue $projectXml 'ApplicationDisplayVersion'
$versionCode = Get-ProjectValue $projectXml 'ApplicationVersion'

if ([string]::IsNullOrWhiteSpace($TargetFramework)) {
    $candidates = @()
    $tfms = Get-ProjectValue $projectXml 'TargetFrameworks'
    if (-not [string]::IsNullOrWhiteSpace($tfms)) { $candidates += $tfms.Split(';') | ForEach-Object { $_.Trim() } }
    $tfm = Get-ProjectValue $projectXml 'TargetFramework'
    if (-not [string]::IsNullOrWhiteSpace($tfm)) { $candidates += $tfm.Trim() }

    $TargetFramework = $candidates | Where-Object { $_ -like '*-android*' } | Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($TargetFramework)) {
    Exit-WithError 'No se encontro un TargetFramework Android en el csproj.'
}

$DotnetPath = Resolve-ToolPath $DotnetPath 'dotnet' 'C:\Program Files\dotnet\dotnet.exe'

if ([string]::IsNullOrWhiteSpace($KeystorePath)) {
    $KeystorePath = if (-not [string]::IsNullOrWhiteSpace($env:ANDROID_KEYSTORE_PATH)) {
        $env:ANDROID_KEYSTORE_PATH
    }
    else {
        Join-Path $ProjectRoot 'socratic.keystore'
    }
}
$KeystorePath = if ([System.IO.Path]::IsPathRooted($KeystorePath)) { $KeystorePath } else { Join-Path $ProjectRoot $KeystorePath }
if (-not (Test-Path -LiteralPath $KeystorePath)) {
    Write-Host "Keystore no encontrado: $KeystorePath" -ForegroundColor Yellow
    Write-Host 'Crealo una vez con:' -ForegroundColor Yellow
    Write-Host "  keytool -genkeypair -v -keystore socratic.keystore -alias $KeyAlias -keyalg RSA -keysize 2048 -validity 10000" -ForegroundColor Yellow
    Exit-WithError 'Falta el keystore.'
}
$KeystorePath = (Resolve-Path -LiteralPath $KeystorePath).Path

# Sin valor por defecto: una contrasena hardcodeada en un repositorio publico deja de ser
# una contrasena (constitucion 5).
$passwordFile = Join-Path $ProjectRoot 'keystore.password.txt'
if ([string]::IsNullOrWhiteSpace($StorePass)) {
    $StorePass = if (-not [string]::IsNullOrWhiteSpace($env:ANDROID_KEYSTORE_PASSWORD)) {
        $env:ANDROID_KEYSTORE_PASSWORD
    }
    elseif (Test-Path -LiteralPath $passwordFile) {
        (Get-Content -LiteralPath $passwordFile -Raw).Trim()
    }
    else {
        $null
    }
}
if ([string]::IsNullOrWhiteSpace($StorePass)) {
    Exit-WithError 'No hay contrasena de keystore. Pasa -StorePass, define ANDROID_KEYSTORE_PASSWORD o crea keystore.password.txt.'
}
if ([string]::IsNullOrWhiteSpace($KeyPass)) {
    $KeyPass = if (-not [string]::IsNullOrWhiteSpace($env:ANDROID_KEY_PASSWORD)) { $env:ANDROID_KEY_PASSWORD } else { $StorePass }
}

$releaseOutputPath = Join-Path $ProjectRoot "bin\$Configuration\$TargetFramework"
$debugOutputPath = Join-Path $ProjectRoot "bin\Debug\$TargetFramework"

# El obj del proyecto vive en OneDrive; MSBuild va notablemente mas rapido fuera de el.
if ([string]::IsNullOrWhiteSpace($IntermediateOutputRoot)) {
    $IntermediateOutputRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'FileManager-msbuild-obj'
}
$IntermediateOutputRoot = if ([System.IO.Path]::IsPathRooted($IntermediateOutputRoot)) { $IntermediateOutputRoot } else { Join-Path $ProjectRoot $IntermediateOutputRoot }
New-Item -ItemType Directory -Path $IntermediateOutputRoot -Force | Out-Null
$IntermediateOutputRoot = (Resolve-Path -LiteralPath $IntermediateOutputRoot).Path
if (-not $IntermediateOutputRoot.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
    $IntermediateOutputRoot = "$IntermediateOutputRoot$([System.IO.Path]::DirectorySeparatorChar)"
}

$intermediateArgs = @(
    "-p:BaseIntermediateOutputPath=$IntermediateOutputRoot",
    "-p:MSBuildProjectExtensionsPath=$IntermediateOutputRoot",
    '-p:DefaultItemExcludes="$(DefaultItemExcludes);bin\**;obj\**"'
)
$signingArgs = @(
    '-p:AndroidKeyStore=true',
    "-p:AndroidSigningKeyStore=$KeystorePath",
    "-p:AndroidSigningKeyAlias=$KeyAlias",
    "-p:AndroidSigningKeyPass=$KeyPass",
    "-p:AndroidSigningStorePass=$StorePass"
)

Write-Host '========================================'
Write-Host '   File Manager - Build and Sign Script'
Write-Host '========================================'
Write-Host
Write-Host "Proyecto: $ProjectPath"
Write-Host "Version: $displayVersion (versionCode $versionCode)"
Write-Host "TargetFramework: $TargetFramework"
Write-Host "Package: $packageName"
Write-Host "Keystore: $KeystorePath (alias $KeyAlias)"
Write-Host "Obj temporal: $IntermediateOutputRoot"
Write-Host

Push-Location $ProjectRoot
try {
    if ($Clean -and -not $SkipClean) {
        Write-Host '[1/4] Limpiando proyecto...'
        Invoke-NativeCommand 'Fallo al limpiar el proyecto' $DotnetPath (@('clean', $ProjectPath, '-c', $Configuration, '-f', $TargetFramework) + $intermediateArgs)
    }
    else {
        Write-Host '[1/4] Limpieza omitida. Usa -Clean si quieres forzar dotnet clean.'
    }

    if (-not $SkipApk) {
        Write-Host
        Write-Host '[2/4] Compilando APK firmado para testing...'
        Invoke-NativeCommand 'Fallo al compilar APK' $DotnetPath (@(
            'publish', $ProjectPath,
            '-f', $TargetFramework,
            '-c', 'Debug',
            '-p:AndroidPackageFormat=apk'
        ) + $intermediateArgs + $signingArgs)
    }
    else {
        Write-Host
        Write-Host '[2/4] APK omitido.'
    }

    Write-Host
    Write-Host '[3/4] Compilando AAB firmado para Google Play...'
    Invoke-NativeCommand 'Fallo al compilar AAB' $DotnetPath (@(
        'publish', $ProjectPath,
        '-f', $TargetFramework,
        '-c', $Configuration,
        '-p:AndroidPackageFormat=aab'
    ) + $intermediateArgs + $signingArgs)

    Write-Host
    Write-Host '[4/4] Verificando archivos generados...'

    $signedApk = $null
    if (-not $SkipApk -and (Test-Path -LiteralPath $debugOutputPath)) {
        $signedApk = Get-LatestPackage $debugOutputPath '*-Signed.apk' -PreferPublishFolder
    }

    $signedAab = $null
    if (Test-Path -LiteralPath $releaseOutputPath) {
        $signedAab = Get-LatestPackage $releaseOutputPath '*-Signed.aab' -PreferPublishFolder
    }

    if ($signedApk) {
        Write-Host "OK APK firmado: $($signedApk.FullName)"
        Write-Host ("  Tamano: {0:N1} MB" -f ($signedApk.Length / 1MB))
    }
    elseif (-not $SkipApk) {
        Write-Host 'WARNING: APK firmado no encontrado.' -ForegroundColor Yellow
    }

    if ($signedAab) {
        Write-Host "OK AAB firmado: $($signedAab.FullName)"
        Write-Host ("  Tamano: {0:N1} MB" -f ($signedAab.Length / 1MB))
    }
    else {
        Exit-WithError 'AAB firmado no encontrado.'
    }

    Write-Host
    Write-Host '========================================'
    Write-Host '          COMPILACION COMPLETA'
    Write-Host '========================================'
    if ($signedApk) {
        Write-Host "APK para testing: $($signedApk.FullName)"
    }
    Write-Host "AAB para Google Play: $($signedAab.FullName)"
    Write-Host
    Write-Host 'Siguientes pasos (constitucion 7 y A.5):'
    Write-Host "  1. Validar el AAB en dispositivo real."
    Write-Host "  2. Subir primero al canal de PRUEBAS CERRADAS."
    Write-Host "  3. Comprobar que el versionCode $versionCode es incremental en Play Console."
    Write-Host
    Write-Host 'Para publicar:'
    Write-Host "  .\publish_aab_to_play.ps1 -AabPath `"$($signedAab.FullName)`""
    Pause-Script
}
finally {
    Pop-Location
}
