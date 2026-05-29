# Deploy-ToWorkPC.ps1
# Package TimeTrack for deployment to work PC with Airlock

# Auto-detect project root (looks for .csproj file)
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = $scriptDir

# Walk up directories to find .csproj
while ($projectRoot -and -not (Get-ChildItem -Path $projectRoot -Filter "*.csproj" -ErrorAction SilentlyContinue)) {
    $parent = Split-Path -Parent $projectRoot
    if ($parent -eq $projectRoot) { break } # Reached root
    $projectRoot = $parent
}

if (-not (Get-ChildItem -Path $projectRoot -Filter "*.csproj" -ErrorAction SilentlyContinue)) {
Write-Host "ERROR: Could not find project root (.csproj file)" -ForegroundColor Red
    Write-Host "Current directory: $scriptDir" -ForegroundColor Yellow
    exit 1
}

$publishDir = Join-Path $projectRoot "bin\Release\net8.0-windows\win-x64\publish"
$zipName = Join-Path $projectRoot "TimeTrack-v2.6.0-Deploy.zip"
$readmePath = Join-Path $projectRoot "DEPLOYMENT_INSTRUCTIONS.txt"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  TimeTrack Deployment Packager" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan
Write-Host "Project root: $projectRoot" -ForegroundColor Gray
Write-Host ""

# Verify publish folder exists and is self-contained
if (-not (Test-Path $publishDir)) {
    Write-Host "[ERROR] Publish folder not found. Run first:" -ForegroundColor Red
    Write-Host "  cd '$projectRoot'" -ForegroundColor Yellow
    Write-Host "  dotnet publish -c Release -r win-x64 --self-contained" -ForegroundColor Yellow
    exit 1
}

$totalSize = (Get-ChildItem $publishDir -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB
if ($totalSize -lt 100) {
    Write-Host "[ERROR] Publish folder is too small ($([math]::Round($totalSize,2)) MB)" -ForegroundColor Red
    Write-Host "This indicates a framework-dependent build." -ForegroundColor Yellow
    Write-Host "Run: .\Diagnostic-PublishFolder.ps1 for details" -ForegroundColor Yellow
    exit 1
}

Write-Host "[OK] Publish folder validated ($([math]::Round($totalSize,2)) MB)" -ForegroundColor Green

# Remove old zip if exists
if (Test-Path $zipName) {
    Write-Host "`nRemoving old deployment package..." -ForegroundColor Yellow
    Remove-Item $zipName -Force
}

# Create deployment package
Write-Host "Creating deployment package..." -ForegroundColor Yellow
Compress-Archive -Path "$publishDir\*" -DestinationPath $zipName -CompressionLevel Optimal -Force

$zipSize = (Get-Item $zipName).Length / 1MB
Write-Host "[OK] Package created: $zipName ($([math]::Round($zipSize,2)) MB)" -ForegroundColor Green

# Create deployment instructions
$instructions = @"
# TimeTrack v2.6.0 - Deployment Instructions for Work PC with Airlock

## CRITICAL: Avoid OneDrive Paths!

Airlock Digital blocks execution from OneDrive-synced folders. You MUST extract to a LOCAL path.

### ? DO NOT extract to these locations:
- Documents (often OneDrive-synced)
- Desktop (often OneDrive-synced)
- Any folder with OneDrive icon
- Network shares (unless approved by IT)

### ? EXTRACT TO one of these locations:
1. **C:\Apps\TimeTrack** (Recommended - create if needed)
2. **C:\Tools\TimeTrack** (Alternative)
3. **C:\Users\<YourUsername>\AppData\Local\TimeTrack** (Hidden but safe)
4. **Any local C:\ path that is NOT synced to OneDrive**

## Deployment Steps

### Step 1: Transfer the Package
Copy $zipName to your work PC via:
- Email attachment
- USB drive
- Network share (if allowed)
- Corporate file transfer tool

### Step 2: Extract to LOCAL Path

**PowerShell method (Recommended):**
``````powershell
# Create local Apps folder if it doesn't exist
New-Item -ItemType Directory -Path "C:\Apps\TimeTrack" -Force

# Extract the zip
Expand-Archive -Path "C:\path\to\$zipName" -DestinationPath "C:\Apps\TimeTrack" -Force

# Verify extraction
Get-ChildItem "C:\Apps\TimeTrack" | Measure-Object
# Should show 100+ files
``````

**Windows Explorer method:**
1. Create folder: C:\Apps\TimeTrack
2. Right-click $zipName ? Extract All
3. Extract to: C:\Apps\TimeTrack

### Step 3: Run the Application
1. Navigate to C:\Apps\TimeTrack
2. Double-click TimeTrackv2.exe
3. Application should start without prompting for .NET runtime

## Troubleshooting

### Issue: ".NET Desktop Runtime Required" prompt appears
**Cause:** Wrong folder extracted or incomplete extraction
**Solution:**
``````powershell
# Check if all runtime files are present
Test-Path "C:\Apps\TimeTrack\coreclr.dll"
Test-Path "C:\Apps\TimeTrack\hostfxr.dll"
# Should both return True
``````

If False, re-extract the entire ZIP to a fresh folder.

### Issue: Airlock blocks execution
**Symptom:** App doesn't start, or access denied error
**Solutions:**

1. **Verify you're NOT in OneDrive:**
   ``````powershell
   # Check current folder
   Get-Item "C:\Apps\TimeTrack" | Select-Object FullName, Attributes
   # Should NOT show "ReparsePoint" (OneDrive placeholder)
   ``````

2. **Try a different local path:**
   - C:\Tools\TimeTrack
   - C:\Users\<YourUsername>\AppData\Local\Programs\TimeTrack

3. **Request IT Exception:**
   - Get the file hash:
     ``````powershell
     cd "C:\Apps\TimeTrack"
     (Get-FileHash .\TimeTrackv2.exe -Algorithm SHA256).Hash
     ``````
   - Provide hash to IT for whitelisting
   - OR request folder exception for C:\Apps\TimeTrack

4. **Check Event Viewer for block details:**
   ``````powershell
   # Check for AppLocker/Airlock blocks
   Get-WinEvent -FilterHashtable @{
       LogName = 'Microsoft-Windows-AppLocker/EXE and DLL'
 StartTime = (Get-Date).AddMinutes(-10)
   } | Where-Object { `$_.Message -like "*TimeTrack*" } | Format-List
   ``````

### Issue: Application crashes on startup
**Check error log:**
``````powershell
notepad "$env:APPDATA\TimeTrack v2\time_track_log.txt"
``````

**Check Windows Event Log:**
``````powershell
Get-EventLog -LogName Application -Source ".NET Runtime" -Newest 5 | Format-List
``````

## Data Storage

TimeTrack stores data in your user profile (NOT beside the executable):
- Database: %APPDATA%\TimeTrack v2\timetrack_v2.db
- Logs: %APPDATA%\TimeTrack v2\time_track_log.txt
- Settings: %APPDATA%\TimeTrack v2\timetrack_settings.xml

**Benefits:**
? Data survives application updates
? Data backed up with user profile
? No write permissions needed in application folder
? Works regardless of where you extract the app

## Quick Validation Commands

After extraction, run these to verify:

``````powershell
# 1. Navigate to extracted folder
cd "C:\Apps\TimeTrack"

# 2. Check for runtime DLLs
@("coreclr.dll", "hostfxr.dll", "hostpolicy.dll", "TimeTrackv2.exe") | ForEach-Object {
    if (Test-Path `$_) { Write-Host "? `$_" -ForegroundColor Green }
    else { Write-Host "? `$_" -ForegroundColor Red }
}

# 3. Check folder is NOT OneDrive
if ((Get-Item .).Attributes -match "ReparsePoint") {
    Write-Host "? WARNING: This folder appears to be OneDrive-synced!" -ForegroundColor Yellow
} else {
    Write-Host "? Folder is local (not OneDrive)" -ForegroundColor Green
}

# 4. Try to run
.\TimeTrackv2.exe
``````

## Version Information
- Version: 2.6.0
- Build Date: $(Get-Date -Format "yyyy-MM-dd HH:mm")
- Target Framework: .NET 8.0
- Runtime: Self-contained (win-x64)
- Package Size: $([math]::Round($zipSize,2)) MB
- Extracted Size: ~$([math]::Round($totalSize,2)) MB

## Support
For issues, provide:
1. Extraction path (e.g., C:\Apps\TimeTrack)
2. Output from Quick Validation Commands above
3. Contents of: `%APPDATA%\TimeTrack v2\time_track_log.txt`
4. Windows Event Viewer errors (if any)
"@

$readmePath = "DEPLOYMENT_INSTRUCTIONS.txt"
$instructions | Out-File $readmePath -Encoding UTF8 -Force

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Deployment Package Ready!" -ForegroundColor Green
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "Package: $zipName" -ForegroundColor White
Write-Host "Instructions: $readmePath" -ForegroundColor White

Write-Host "`n??  IMPORTANT FOR WORK PC:" -ForegroundColor Yellow -BackgroundColor DarkRed
Write-Host "?????????????????????????????????????????" -ForegroundColor Yellow
Write-Host "Extract to LOCAL path, NOT OneDrive!" -ForegroundColor Yellow
Write-Host "  ? C:\Apps\TimeTrack" -ForegroundColor Green
Write-Host "  ? C:\Tools\TimeTrack" -ForegroundColor Green
Write-Host "  ? Documents (OneDrive)" -ForegroundColor Red
Write-Host "  ? Desktop (OneDrive)" -ForegroundColor Red
Write-Host "?????????????????????????????????????????" -ForegroundColor Yellow

Write-Host "`nDeployment steps:" -ForegroundColor Cyan
Write-Host "1. Transfer $zipName to work PC" -ForegroundColor White
Write-Host "2. Extract to C:\Apps\TimeTrack (or similar LOCAL path)" -ForegroundColor White
Write-Host "3. Run TimeTrackv2.exe" -ForegroundColor White

Write-Host "`nIf still blocked by Airlock:" -ForegroundColor Yellow
Write-Host "- Verify extraction path is NOT in OneDrive" -ForegroundColor White
Write-Host "- Try C:\Users\<You>\AppData\Local\TimeTrack" -ForegroundColor White
Write-Host "- Request IT to whitelist the folder" -ForegroundColor White
Write-Host "- See $readmePath for full troubleshooting" -ForegroundColor White

# Offer to open the folder
Write-Host "`nOpen deployment folder? (Y/N): " -ForegroundColor Cyan -NoNewline
$response = Read-Host
if ($response -eq 'Y' -or $response -eq 'y') {
    Start-Process explorer.exe -ArgumentList (Get-Location).Path
}

Write-Host ""
