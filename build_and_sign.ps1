<#
.SYNOPSIS
    Compila y firma File Manager para Android (constitucion 6 y 7).

.DESCRIPTION
    Genera un APK y un AAB firmados y reproducibles a partir del codigo fuente.
    La contrasena del keystore nunca se guarda en el repositorio: se pasa por parametro
    o por la variable de entorno FILEMANAGER_KEYSTORE_PASSWORD (constitucion 5).

.EXAMPLE
    $env:FILEMANAGER_KEYSTORE_PASSWORD = "..."
    ./build_and_sign.ps1

.EXAMPLE
    ./build_and_sign.ps1 -KeystorePassword (Read-Host -AsSecureString "Keystore password")
#>
[CmdletBinding()]
param(
    [string]$Keystore = "socratic.keystore",
    [string]$KeyAlias = "socratic",
    [string]$KeystorePassword = $env:FILEMANAGER_KEYSTORE_PASSWORD,
    [string]$Configuration = "Release",
    [string]$Framework = "net9.0-android36.0",
    [string]$OutputDir = "artifacts"
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($KeystorePassword)) {
    throw "No keystore password provided. Set FILEMANAGER_KEYSTORE_PASSWORD or pass -KeystorePassword."
}

if (-not (Test-Path $Keystore)) {
    Write-Host "Keystore '$Keystore' not found. Create it once with:" -ForegroundColor Yellow
    Write-Host "  keytool -genkeypair -v -keystore $Keystore -alias $KeyAlias -keyalg RSA -keysize 2048 -validity 10000" -ForegroundColor Yellow
    throw "Missing keystore."
}

# Version declarada en el csproj: es la unica fuente de verdad (constitucion 6).
[xml]$project = Get-Content "FileManager.csproj"
$displayVersion = $project.Project.PropertyGroup.ApplicationDisplayVersion | Where-Object { $_ } | Select-Object -First 1
$versionCode = $project.Project.PropertyGroup.ApplicationVersion | Where-Object { $_ } | Select-Object -First 1

Write-Host "Building File Manager $displayVersion (versionCode $versionCode)" -ForegroundColor Cyan

# Compilacion limpia: garantiza que el artefacto se puede regenerar desde cero (constitucion 3).
dotnet clean -c $Configuration -f $Framework | Out-Null
if ($LASTEXITCODE -ne 0) { throw "dotnet clean failed." }

New-Item -ItemType Directory -Force $OutputDir | Out-Null

$common = @(
    "-c", $Configuration,
    "-f", $Framework,
    "-p:AndroidKeyStore=true",
    "-p:AndroidSigningKeyStore=$Keystore",
    "-p:AndroidSigningKeyAlias=$KeyAlias",
    "-p:AndroidSigningKeyPass=$KeystorePassword",
    "-p:AndroidSigningStorePass=$KeystorePassword",
    "-p:OutputPath=$OutputDir/"
)

Write-Host "Publishing AAB…" -ForegroundColor Cyan
dotnet publish @common -p:AndroidPackageFormat=aab
if ($LASTEXITCODE -ne 0) { throw "AAB publish failed." }

Write-Host "Publishing APK…" -ForegroundColor Cyan
dotnet publish @common -p:AndroidPackageFormat=apk
if ($LASTEXITCODE -ne 0) { throw "APK publish failed." }

$artifacts = Get-ChildItem -Path $OutputDir -Recurse -Include *.aab, *-Signed.apk |
    Sort-Object LastWriteTime -Descending

if (-not $artifacts) { throw "Build reported success but produced no signed artifacts." }

Write-Host "`nSigned artifacts:" -ForegroundColor Green
$artifacts | ForEach-Object {
    "{0,-60} {1,8:N1} MB" -f $_.FullName, ($_.Length / 1MB) | Write-Host
}

Write-Host "`nNext steps (constitucion 7):" -ForegroundColor Cyan
Write-Host "  1. Validate the AAB on a real device."
Write-Host "  2. Upload to the CLOSED TESTING track first."
Write-Host "  3. Check versionCode $versionCode is incremental in Play Console."
