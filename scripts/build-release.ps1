#Requires -Version 5.1
<#
.SYNOPSIS
    Builds DockBar Release/x64, packages artifacts, and optionally publishes a GitHub Release.

.DESCRIPTION
    1. Builds DockBar.csproj (Release / x64)
    2. Exports the signing certificate as DockBar.cer
    3. Copies DockBar.msix to release/
    4. Generates release/install.ps1 for end-user installation
    5. Optionally creates a GitHub Release and uploads the files (requires gh CLI)

.PARAMETER Version
    Tag name for the GitHub Release (e.g. "v1.0.3").
    If omitted, artifacts are built locally but not published.

.PARAMETER PfxPassword
    Password for DockBar_TemporaryKey.pfx. Defaults to empty string (no password).
#>
[CmdletBinding()]
param(
    [string]$Version    = "",
    [string]$PfxPassword = ""
)

$ErrorActionPreference = 'Stop'

$root       = Split-Path $PSScriptRoot -Parent
$projFile   = Join-Path $root "DockBar\DockBar.csproj"
$pfxFile    = Join-Path $root "DockBar\DockBar_TemporaryKey.pfx"
$releaseDir = Join-Path $root "release"

# ---------------------------------------------------------------------------
# 1. Clean / recreate release/
# ---------------------------------------------------------------------------
if (Test-Path $releaseDir) {
    Remove-Item $releaseDir -Recurse -Force
}
New-Item -ItemType Directory -Path $releaseDir | Out-Null
Write-Host "release/ folder ready."

# ---------------------------------------------------------------------------
# 2. Locate MSBuild
# ---------------------------------------------------------------------------
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild  = $null

if (Test-Path $vswhere) {
    $msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild `
        -find "MSBuild\**\Bin\MSBuild.exe" 2>$null | Select-Object -First 1
}

if (-not $msbuild -or -not (Test-Path $msbuild)) {
    $fallback = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
    if (Test-Path $fallback) {
        $msbuild = $fallback
    } else {
        throw "MSBuild not found. Install Visual Studio 2022 or add MSBuild to PATH."
    }
}
Write-Host "Using MSBuild: $msbuild"

# ---------------------------------------------------------------------------
# 3. Build Release / x64
# ---------------------------------------------------------------------------
Write-Host "`nBuilding DockBar (Release / x64)..."
& $msbuild $projFile /p:Configuration=Release /p:Platform=x64 /t:Build /v:minimal
if ($LASTEXITCODE -ne 0) {
    throw "MSBuild failed with exit code $LASTEXITCODE."
}
Write-Host "Build succeeded."

# ---------------------------------------------------------------------------
# 4. Pack .msix with makeappx.exe + sign with signtool.exe
# ---------------------------------------------------------------------------
$buildOut = Join-Path $root "DockBar\bin\x64\Release\net9.0-windows10.0.22000.0\win-x64"
$msixOut  = Join-Path $releaseDir "DockBar.msix"

# Find makeappx.exe (prefer highest SDK version, x64)
$makeappx = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin" `
    -Filter "makeappx.exe" -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\x64\\' } |
    Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName

if (-not $makeappx) { throw "makeappx.exe not found. Install Windows SDK." }
Write-Host "Using makeappx: $makeappx"

# Find signtool.exe
$signtool = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin" `
    -Filter "signtool.exe" -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\x64\\' } |
    Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName

if (-not $signtool) { throw "signtool.exe not found. Install Windows SDK." }
Write-Host "Using signtool: $signtool"

# Pack
Write-Host "`nPacking MSIX..."
& $makeappx pack /d $buildOut /p $msixOut /o /v
if ($LASTEXITCODE -ne 0) { throw "makeappx failed with exit code $LASTEXITCODE." }

# Sign
Write-Host "Signing MSIX..."
& $signtool sign /fd SHA256 /a /f $pfxFile /p $PfxPassword $msixOut
if ($LASTEXITCODE -ne 0) { throw "signtool failed with exit code $LASTEXITCODE." }

Write-Host "Packed + signed -> release\DockBar.msix"

# ---------------------------------------------------------------------------
# 5. Export .cer (public key only) from the PFX
# ---------------------------------------------------------------------------
if (-not (Test-Path $pfxFile)) {
    throw "PFX not found: $pfxFile"
}

$securePassword = if ($PfxPassword) {
    ConvertTo-SecureString $PfxPassword -AsPlainText -Force
} else {
    [System.Security.SecureString]::new()
}

$cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2(
    $pfxFile,
    $securePassword,
    [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::DefaultKeySet
)

$cerPath = Join-Path $releaseDir "DockBar.cer"
Export-Certificate -Cert $cert -FilePath $cerPath -Type CERT | Out-Null
Write-Host "Exported -> release\DockBar.cer  (Subject: $($cert.Subject))"

# ---------------------------------------------------------------------------
# 6. Generate release/install.ps1
# ---------------------------------------------------------------------------
$installScript = @'
#Requires -Version 5.1
<#
.SYNOPSIS
    Installs DockBar Claude Usage extension for PowerToys Command Palette.

.DESCRIPTION
    Registers the self-signed certificate in the local machine trust store
    and installs the DockBar.msix package.
    Must be run as Administrator.
#>

$ErrorActionPreference = 'Stop'

# --- Admin check ---
$isAdmin = ([Security.Principal.WindowsPrincipal]
            [Security.Principal.WindowsIdentity]::GetCurrent()
           ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host ""
    Write-Host "ERROR: Administrator privileges required." -ForegroundColor Red
    Write-Host ""
    Write-Host "Please re-run this script as Administrator:" -ForegroundColor Yellow
    Write-Host "  1. Right-click install.ps1" -ForegroundColor Yellow
    Write-Host "  2. Select 'Run as Administrator'" -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

$scriptDir = Split-Path $MyInvocation.MyCommand.Path -Parent
$cerPath   = Join-Path $scriptDir "DockBar.cer"
$msixPath  = Join-Path $scriptDir "DockBar.msix"

# --- Validate files ---
foreach ($f in @($cerPath, $msixPath)) {
    if (-not (Test-Path $f)) {
        Write-Host "ERROR: Required file not found: $f" -ForegroundColor Red
        exit 1
    }
}

try {
    # --- Trust the certificate ---
    Write-Host "Registering certificate in Cert:\LocalMachine\TrustedPeople ..."
    Import-Certificate -FilePath $cerPath -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
    Write-Host "Certificate registered successfully." -ForegroundColor Green

    # --- Install the MSIX ---
    Write-Host "Installing DockBar.msix ..."
    Add-AppxPackage -Path $msixPath
    Write-Host ""
    Write-Host "Installation complete!" -ForegroundColor Green
    Write-Host "Please restart PowerToys to load the DockBar extension." -ForegroundColor Cyan
}
catch {
    Write-Host ""
    Write-Host "ERROR: Installation failed." -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
'@

$installPath = Join-Path $releaseDir "install.ps1"
Set-Content -Path $installPath -Value $installScript -Encoding UTF8
Write-Host "Generated -> release\install.ps1"

# ---------------------------------------------------------------------------
# Done — local artifacts
# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "=== Release artifacts ready ===" -ForegroundColor Green
Get-ChildItem $releaseDir | Format-Table Name, Length, LastWriteTime -AutoSize

# ---------------------------------------------------------------------------
# 7. (Optional) Publish GitHub Release via gh CLI
# ---------------------------------------------------------------------------
if (-not $Version) {
    Write-Host "No -Version specified. Skipping GitHub Release upload." -ForegroundColor Yellow
    Write-Host "To publish: .\scripts\build-release.ps1 -Version v1.0.3"
    exit 0
}

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "gh CLI not found. Install from https://cli.github.com/ or omit -Version to skip upload."
}

Write-Host "`nPublishing GitHub Release $Version ..."

# Create git tag if it doesn't exist
$existingTag = git tag -l $Version
if (-not $existingTag) {
    Write-Host "Creating tag $Version ..."
    git tag $Version
    git push origin $Version
} else {
    Write-Host "Tag $Version already exists."
}

# Create release and upload files
gh release create $Version `
    "$releaseDir\DockBar.msix" `
    "$releaseDir\DockBar.cer" `
    "$releaseDir\install.ps1" `
    --title $Version `
    --generate-notes

if ($LASTEXITCODE -ne 0) {
    throw "gh release create failed."
}

Write-Host ""
Write-Host "GitHub Release $Version published!" -ForegroundColor Green
