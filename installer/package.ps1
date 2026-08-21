# ============================================================================
#  voboX one-click package builder
#  ============================================================================
#  HOW TO RELEASE A NEW VERSION:
#    1. Edit the version number on the $Version line below (the ONLY place).
#    2. Double-click package.bat at the project root (or run this script).
#
#  The script automatically:
#    - syncs the version into voboX.csproj / voboX.iss / license.txt
#    - regenerates license.rtf from license.txt (keeps pure-ASCII RTF)
#    - stops a running voboX app, checks for a locked installer
#    - publishes a self-contained single-file exe (win-x64)
#    - compiles the Inno Setup installer into the release\ folder
#    - deletes the intermediate release\voboX.exe and opens release\
#
#  NOTE: keep this file pure ASCII (Windows PowerShell 5.1 + UTF-8 no-BOM
#        does not parse Chinese string literals reliably).
# ============================================================================

param(
    [switch]$Force,    # kill a running voboX-Setup installer that blocks the build
    [switch]$SkipOpen  # do not open the release folder at the end
)

$ErrorActionPreference = 'Stop'

# ================= EDIT VERSION HERE =================
$Version = "1.0.2"
# =====================================================

$scriptDir  = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir    = Split-Path -Parent $scriptDir
$releaseDir = Join-Path $rootDir 'release'
$csproj     = Join-Path $rootDir 'voboX.csproj'
$iss        = Join-Path $scriptDir 'voboX.iss'
$lic        = Join-Path $scriptDir 'license.txt'

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    Write-Host "[ERROR] Invalid version: '$Version' (expected e.g. 1.0.2)" -ForegroundColor Red
    exit 1
}

Write-Host "=== voboX package builder (version $Version) ===" -ForegroundColor Cyan

# ---------------------------------------------------------------------------
# 1. Sync version into project files (preserve each file's encoding/BOM)
# ---------------------------------------------------------------------------
Write-Host "[1/6] Syncing version into csproj / iss / license.txt ..."

$csprojLines = [System.IO.File]::ReadAllLines($csproj, [System.Text.Encoding]::UTF8)
for ($i = 0; $i -lt $csprojLines.Count; $i++) {
    if ($csprojLines[$i] -match '^\s*<Version>')            { $csprojLines[$i] = "    <Version>$Version</Version>" }
    elseif ($csprojLines[$i] -match '^\s*<AssemblyVersion>') { $csprojLines[$i] = "    <AssemblyVersion>$Version.0</AssemblyVersion>" }
    elseif ($csprojLines[$i] -match '^\s*<FileVersion>')     { $csprojLines[$i] = "    <FileVersion>$Version.0</FileVersion>" }
}
[System.IO.File]::WriteAllLines($csproj, $csprojLines, [System.Text.UTF8Encoding]::new($true))  # csproj has BOM

$issLines = [System.IO.File]::ReadAllLines($iss, [System.Text.Encoding]::UTF8)
for ($i = 0; $i -lt $issLines.Count; $i++) {
    if ($issLines[$i] -match '^#define MyAppVersion') { $issLines[$i] = "#define MyAppVersion `"$Version`"" }
}
[System.IO.File]::WriteAllLines($iss, $issLines, [System.Text.UTF8Encoding]::new($true))  # iss has BOM

$licLines = [System.IO.File]::ReadAllLines($lic, [System.Text.Encoding]::UTF8)
for ($i = 0; $i -lt $licLines.Count; $i++) {
    # keep the Chinese prefix (read from the file) and only swap the trailing digits
    $m = [regex]::Match($licLines[$i], '^(\D*?)(\d+\.\d+\.\d+)\s*$')
    if ($m.Success) { $licLines[$i] = $m.Groups[1].Value + $Version }
}
[System.IO.File]::WriteAllLines($lic, $licLines, [System.Text.UTF8Encoding]::new($false))  # license.txt has no BOM

Write-Host "       Version now set in csproj, voboX.iss, license.txt."

# ---------------------------------------------------------------------------
# 2. Stop a running voboX app (it locks voboX.exe / the installer output)
# ---------------------------------------------------------------------------
Write-Host "[2/6] Stopping running voboX app ..."
Get-Process voboX -ErrorAction SilentlyContinue | Stop-Process -Force

# ---------------------------------------------------------------------------
# 3. Check for a running installer (it locks the setup exe we must overwrite)
# ---------------------------------------------------------------------------
Write-Host "[3/6] Checking for a running installer ..."
$runningSetup = Get-Process -Name 'voboX-Setup*' -ErrorAction SilentlyContinue
if ($runningSetup) {
    $ids = ($runningSetup | ForEach-Object { $_.Id }) -join ','
    Write-Host "[WARN] voboX-Setup is running (PID $ids)." -ForegroundColor Yellow
    if ($Force) {
        $runningSetup | Stop-Process -Force
        Write-Host "       Killed by -Force."
    }
    else {
        Write-Host "[ERROR] Close the running installer first, or rerun with -Force." -ForegroundColor Red
        exit 1
    }
}

# ---------------------------------------------------------------------------
# 4. Regenerate license.rtf from the updated license.txt
# ---------------------------------------------------------------------------
Write-Host "[4/6] Regenerating license.rtf ..."
& (Join-Path $scriptDir 'gen-license.ps1')

# ---------------------------------------------------------------------------
# 5. Publish self-contained single-file exe
# ---------------------------------------------------------------------------
Write-Host "[5/6] Publishing self-contained single-file exe (win-x64) ..."
dotnet publish $csproj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None -p:DebugSymbols=false -o $releaseDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }

# ---------------------------------------------------------------------------
# 6. Compile the Inno Setup installer, then clean up the intermediate exe
# ---------------------------------------------------------------------------
Write-Host "[6/6] Compiling installer with Inno Setup ..."
$iscc = Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'
if (-not (Test-Path $iscc)) { throw "ISCC.exe not found at $iscc" }
& $iscc (Join-Path $scriptDir 'voboX.iss')
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compile failed (exit $LASTEXITCODE)" }

Remove-Item (Join-Path $releaseDir 'voboX.exe') -ErrorAction SilentlyContinue

$setup = Join-Path $releaseDir "voboX-Setup-$Version.exe"
Write-Host ""
Write-Host "=== Done ===" -ForegroundColor Green
Write-Host "Installer: $setup"
if (Test-Path $setup) {
    $mb = [math]::Round((Get-Item $setup).Length / 1MB, 1)
    Write-Host "Size:      $mb MB"
}
if (-not $SkipOpen) { Start-Process explorer.exe $releaseDir }
