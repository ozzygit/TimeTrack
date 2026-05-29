# Create-PortablePackage.ps1
# Creates a portable package ready for USB/network distribution

param(
    [string]$OutputPath = "TimeTrack-Portable"
)

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  TimeTrack Portable Package Creator" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan
Write-Host "??  IMPORTANT: This creates a SELF-CONTAINED build" -ForegroundColor Yellow
Write-Host "   No .NET Runtime installation required on target PC" -ForegroundColor Gray
Write-Host ""

# Auto-detect project root
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = $scriptDir

while ($projectRoot -and -not (Get-ChildItem -Path $projectRoot -Filter "*.csproj" -ErrorAction SilentlyContinue)) {
    $parent = Split-Path -Parent $projectRoot
    if ($parent -eq $projectRoot) { break }
    $projectRoot = $parent
}

if (-not (Get-ChildItem -Path $projectRoot -Filter "*.csproj" -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: Could not find project root (.csproj file)" -ForegroundColor Red
    exit 1
}

# Get version from project file
$csprojPath = Get-ChildItem -Path $projectRoot -Filter "*.csproj" | Select-Object -First 1
$csprojContent = Get-Content $csprojPath.FullName -Raw
if ($csprojContent -match '<Version>([^<]+)</Version>') {
    $version = $matches[1]
} else {
    $version = "Unknown"
}

Write-Host "Version: $version" -ForegroundColor Gray
Write-Host "Project: $projectRoot`n" -ForegroundColor Gray

# Step 1: Build/Publish
$publishDir = Join-Path $projectRoot "bin\publish\win-x64"

Write-Host "[1/5] Publishing self-contained release..." -ForegroundColor Yellow
Push-Location $projectRoot
dotnet clean --configuration Release | Out-Null
dotnet publish -p:PublishProfile=Win64-SelfContained
Pop-Location

if ($LASTEXITCODE -ne 0) {
    Write-Host "? Publish failed!" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $publishDir)) {
    Write-Host "? Publish directory not found: $publishDir" -ForegroundColor Red
    exit 1
}

Write-Host "? Publish successful" -ForegroundColor Green

# Step 2: Verify self-contained
Write-Host "`n[2/5] Verifying build is self-contained..." -ForegroundColor Yellow

$runtimeConfigPath = Join-Path $publishDir "TimeTrackv2.runtimeconfig.json"
if (Test-Path $runtimeConfigPath) {
    $rc = Get-Content $runtimeConfigPath -Raw | ConvertFrom-Json
    if ($rc.runtimeOptions.includedFrameworks) {
        Write-Host "? Build is self-contained" -ForegroundColor Green
    } else {
        Write-Host "? Build is framework-dependent!" -ForegroundColor Red
  exit 1
    }
} else {
    Write-Host "? Runtime config not found!" -ForegroundColor Red
    exit 1
}

# Step 3: Create package directory
$packageDir = Join-Path $projectRoot $OutputPath
$packageWinDir = Join-Path $packageDir "win-x64"

Write-Host "`n[3/5] Creating package directory..." -ForegroundColor Yellow

if (Test-Path $packageDir) {
    Write-Host "Removing existing package..." -ForegroundColor Gray
  Remove-Item $packageDir -Recurse -Force
}

New-Item -ItemType Directory -Path $packageWinDir -Force | Out-Null
Write-Host "? Created: $packageDir" -ForegroundColor Green

# Step 4: Copy files
Write-Host "`n[4/5] Copying files to package..." -ForegroundColor Yellow

# Copy all published files
Copy-Item "$publishDir\*" -Destination $packageWinDir -Recurse -Force
Write-Host "? Copied application files" -ForegroundColor Green

# Copy deployment script
Copy-Item (Join-Path $projectRoot "Deploy-FromUSB.ps1") -Destination $packageWinDir -Force
Write-Host "? Copied deployment script" -ForegroundColor Green

# Copy installation instructions
Copy-Item (Join-Path $projectRoot "INSTALLATION_README.md") -Destination $packageDir -Force
Write-Host "? Copied instructions" -ForegroundColor Green

# Create a simple README.txt at root
$readmeContent = @"
TimeTrack v2 - Version $version

IMPORTANT: Do NOT run TimeTrackv2.exe directly from USB!

Installation Instructions:
==========================

1. Copy the 'win-x64' folder to your computer's C: drive
   (e.g., C:\Apps\TimeTrack)

2. Open PowerShell in that folder

3. Run: .\Deploy-FromUSB.ps1
   (Or manually run TimeTrackv2.exe from the local folder)

For detailed instructions, see INSTALLATION_README.md

"@

$readmeContent | Out-File (Join-Path $packageDir "README.txt") -Encoding UTF8
Write-Host "? Created README.txt" -ForegroundColor Green

# Step 5: Create ZIP archive
Write-Host "`n[5/5] Creating ZIP archive..." -ForegroundColor Yellow

$zipName = "TimeTrack-v$version-Portable.zip"
$zipPath = Join-Path $projectRoot $zipName

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Compress-Archive -Path "$packageDir\*" -DestinationPath $zipPath -CompressionLevel Optimal
Write-Host "? Created: $zipName" -ForegroundColor Green

# Summary
$zipSize = (Get-Item $zipPath).Length
$fileCount = (Get-ChildItem $packageWinDir -Recurse -File).Count

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "? Package created successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "`nPackage Details:" -ForegroundColor White
Write-Host "  Version: $version" -ForegroundColor Gray
Write-Host "  Files: $fileCount" -ForegroundColor Gray
Write-Host "  ZIP size: $([math]::Round($zipSize / 1MB, 1)) MB" -ForegroundColor Gray
Write-Host "  Location: $zipPath" -ForegroundColor Gray

Write-Host "`nContents:" -ForegroundColor White
Write-Host "  - win-x64\ (application files)" -ForegroundColor Gray
Write-Host "  - Deploy-FromUSB.ps1 (automated installer)" -ForegroundColor Gray
Write-Host "  - INSTALLATION_README.md (detailed instructions)" -ForegroundColor Gray
Write-Host "  - README.txt (quick start guide)" -ForegroundColor Gray

Write-Host "`nNext Steps:" -ForegroundColor Yellow
Write-Host "  1. Copy $zipName to USB drive" -ForegroundColor White
Write-Host "  2. Extract on target PC" -ForegroundColor White
Write-Host "  3. Read README.txt for installation instructions" -ForegroundColor White

Write-Host "`n??  Remember: Application must be copied to local drive before running!" -ForegroundColor Yellow

# Ask to open folder
Write-Host ""
$response = Read-Host "Open package folder? (y/n)"
if ($response -eq 'y') {
    Start-Process explorer.exe (Split-Path $zipPath)
}
