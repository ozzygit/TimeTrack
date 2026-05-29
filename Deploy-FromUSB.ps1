# Deploy-FromUSB.ps1
# Copies TimeTrack from USB to local drive and runs it

param(
    [string]$USBPath,
    [string]$DestinationPath = "C:\Apps\TimeTrack"
)

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  TimeTrack USB to Local Deployment" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# If no USB path provided, try to find it
if (-not $USBPath) {
    Write-Host "Searching for TimeTrack on removable drives..." -ForegroundColor Yellow
    
    $removableDrives = Get-Volume | Where-Object { $_.DriveType -eq 'Removable' -and $_.DriveLetter }
    
    foreach ($drive in $removableDrives) {
        $testPath = "$($drive.DriveLetter):\win-x64\TimeTrackv2.exe"
        if (Test-Path $testPath) {
            $USBPath = "$($drive.DriveLetter):\win-x64"
     Write-Host "? Found TimeTrack on drive $($drive.DriveLetter):" -ForegroundColor Green
            break
      }
    }
    
    if (-not $USBPath) {
        Write-Host "? Could not find TimeTrack on any USB drive" -ForegroundColor Red
   Write-Host "`nPlease specify the path manually:" -ForegroundColor Yellow
    Write-Host "  .\Deploy-FromUSB.ps1 -USBPath 'E:\win-x64'" -ForegroundColor White
   exit 1
    }
}

# Validate source
if (-not (Test-Path "$USBPath\TimeTrackv2.exe")) {
    Write-Host "? TimeTrackv2.exe not found at: $USBPath" -ForegroundColor Red
    exit 1
}

# Count files to copy
$sourceFiles = Get-ChildItem $USBPath -Recurse -File
$totalSize = ($sourceFiles | Measure-Object -Property Length -Sum).Sum

Write-Host "Source: $USBPath" -ForegroundColor Gray
Write-Host "Destination: $DestinationPath" -ForegroundColor Gray
Write-Host "Files to copy: $($sourceFiles.Count)" -ForegroundColor Gray
Write-Host "Total size: $([math]::Round($totalSize / 1MB, 1)) MB`n" -ForegroundColor Gray

# Create destination directory
if (-not (Test-Path $DestinationPath)) {
    Write-Host "Creating destination directory..." -ForegroundColor Yellow
    try {
        New-Item -ItemType Directory -Path $DestinationPath -Force | Out-Null
    Write-Host "? Created: $DestinationPath" -ForegroundColor Green
    } catch {
   Write-Host "? Failed to create directory: $($_.Exception.Message)" -ForegroundColor Red
     Write-Host "`nYou may need administrator privileges to create folders in C:\Apps" -ForegroundColor Yellow
 Write-Host "Try running this script as Administrator, or use a different path:" -ForegroundColor Yellow
      Write-Host "  .\Deploy-FromUSB.ps1 -DestinationPath 'C:\Users\$env:USERNAME\TimeTrack'" -ForegroundColor White
  exit 1
    }
} else {
    Write-Host "? Destination already exists" -ForegroundColor Yellow
    $response = Read-Host "Delete existing files and re-copy? (y/n)"
    if ($response -ne 'y') {
     Write-Host "Cancelled." -ForegroundColor Gray
     exit 0
  }
    
    Write-Host "Removing existing files..." -ForegroundColor Yellow
    Remove-Item "$DestinationPath\*" -Recurse -Force
}

# Copy files
Write-Host "`nCopying files from USB to local drive..." -ForegroundColor Yellow

try {
    Copy-Item "$USBPath\*" -Destination $DestinationPath -Recurse -Force
    Write-Host "? Files copied successfully" -ForegroundColor Green
} catch {
    Write-Host "? Copy failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Verify critical files
Write-Host "`nVerifying installation..." -ForegroundColor Yellow

$criticalFiles = @(
    "TimeTrackv2.exe",
    "TimeTrackv2.dll",
    "hostfxr.dll",
    "coreclr.dll"
)

$allPresent = $true
foreach ($file in $criticalFiles) {
    $filePath = Join-Path $DestinationPath $file
    if (Test-Path $filePath) {
        Write-Host "  ? $file" -ForegroundColor Green
    } else {
        Write-Host "  ? $file - MISSING" -ForegroundColor Red
        $allPresent = $false
    }
}

if (-not $allPresent) {
    Write-Host "`n? Installation incomplete!" -ForegroundColor Red
    exit 1
}

Write-Host "`n? Installation successful!" -ForegroundColor Green
Write-Host "`nTimeTrack is installed at: $DestinationPath" -ForegroundColor White

# Ask to create desktop shortcut
$response = Read-Host "`nCreate desktop shortcut? (y/n)"
if ($response -eq 'y') {
    $WshShell = New-Object -ComObject WScript.Shell
    $desktopPath = [Environment]::GetFolderPath("Desktop")
    $shortcutPath = Join-Path $desktopPath "TimeTrack v2.lnk"
    $shortcut = $WshShell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = Join-Path $DestinationPath "TimeTrackv2.exe"
    $shortcut.WorkingDirectory = $DestinationPath
    $shortcut.Description = "TimeTrack v2 - Time tracking application"
    $shortcut.Save()
    Write-Host "? Shortcut created on desktop" -ForegroundColor Green
}

# Ask to launch
$response = Read-Host "`nLaunch TimeTrack now? (y/n)"
if ($response -eq 'y') {
    Write-Host "Starting TimeTrack..." -ForegroundColor Yellow
    Start-Process -FilePath (Join-Path $DestinationPath "TimeTrackv2.exe") -WorkingDirectory $DestinationPath
    Write-Host "? TimeTrack launched" -ForegroundColor Green
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Deployment complete!" -ForegroundColor Green
Write-Host "Application location: $DestinationPath" -ForegroundColor White
Write-Host "========================================`n" -ForegroundColor Cyan
