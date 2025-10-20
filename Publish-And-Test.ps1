# Publish-And-Test.ps1
# Complete workflow to publish and test TimeTrack from c:\temp

param(
    [switch]$SkipPublish,
    [switch]$OpenLogs
)

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  TimeTrack Publish & Test from c:\temp" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Step 1: Clean and publish (unless skipped)
if (-not $SkipPublish) {
    Write-Host "[1/5] Cleaning previous build..." -ForegroundColor Yellow
    dotnet clean --configuration Release
    
    Write-Host "[2/5] Publishing self-contained executable..." -ForegroundColor Yellow
    dotnet publish -c Release -r win-x64 --self-contained
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "`n? Publish failed!" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "? Publish successful`n" -ForegroundColor Green
} else {
    Write-Host "[Skipped] Using existing published executable`n" -ForegroundColor Gray
}

# Step 2: Copy to c:\temp
Write-Host "[3/5] Copying to C:\temp..." -ForegroundColor Yellow
$publishPath = "bin\Release\net8.0-windows\win-x64\publish\TimeTrack.exe"
$targetPath = "C:\temp\TimeTrack.exe"

if (-not (Test-Path $publishPath)) {
    Write-Host "? Published executable not found at: $publishPath" -ForegroundColor Red
    exit 1
}

# Ensure c:\temp exists
if (-not (Test-Path "C:\temp")) {
    New-Item -ItemType Directory -Path "C:\temp" -Force | Out-Null
}

Copy-Item $publishPath $targetPath -Force
$fileInfo = Get-Item $targetPath
Write-Host "? Copied $($fileInfo.Length / 1MB) MB to $targetPath`n" -ForegroundColor Green

# Step 3: Run from release folder (control test)
Write-Host "[4/5] Testing from release folder (control)..." -ForegroundColor Yellow
$releaseProcess = Start-Process -FilePath $publishPath -PassThru

Start-Sleep -Seconds 3

if ($releaseProcess.HasExited) {
    Write-Host "? Failed to run from release folder (Exit: $($releaseProcess.ExitCode))" -ForegroundColor Red
    Write-Host "   This is a problem with the build itself, not the location.`n" -ForegroundColor Yellow
} else {
    Write-Host "? Running from release folder" -ForegroundColor Green
    Stop-Process -Id $releaseProcess.Id -Force
    Write-Host ""
}

# Step 4: Run from c:\temp (actual test)
Write-Host "[5/5] Testing from C:\temp..." -ForegroundColor Yellow
Write-Host "Watch for error dialogs..." -ForegroundColor Gray

$tempProcess = Start-Process -FilePath $targetPath -PassThru

# Monitor for 5 seconds
$success = $false
for ($i = 0; $i -lt 10; $i++) {
    Start-Sleep -Milliseconds 500
    
    if ($tempProcess.HasExited) {
        Write-Host "`n? Process exited immediately (Exit Code: $($tempProcess.ExitCode))" -ForegroundColor Red
        
        switch ($tempProcess.ExitCode) {
            0 { Write-Host "   Clean exit - may have closed normally" -ForegroundColor Gray }
            1 { Write-Host "   Application startup error - check error dialog" -ForegroundColor Yellow }
            -1 { Write-Host "   Unhandled exception or crash" -ForegroundColor Yellow }
            default { Write-Host "   Unknown exit code" -ForegroundColor Yellow }
        }
        
        break
    }
    
    Write-Host "." -NoNewline -ForegroundColor Green
}

if (-not $tempProcess.HasExited) {
    $success = $true
    Write-Host "`n? Application is running from C:\temp!" -ForegroundColor Green
    Write-Host "   Stopping test process..." -ForegroundColor Gray
    Stop-Process -Id $tempProcess.Id -Force
}

# Results summary
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Test Results" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Check database
$dbPath = "$env:APPDATA\TimeTrack v2\timetrack_v2.db"
if (Test-Path $dbPath) {
    $dbInfo = Get-Item $dbPath
    Write-Host "? Database: $dbPath" -ForegroundColor Green
    Write-Host "   Size: $($dbInfo.Length) bytes, Modified: $($dbInfo.LastWriteTime.ToString('HH:mm:ss'))" -ForegroundColor Gray
} else {
    Write-Host "? Database: Not created" -ForegroundColor Red
}

# Check log file
$logPath = "$env:LOCALAPPDATA\TimeTrack v2\time_track_log.txt"
if (Test-Path $logPath) {
    $logInfo = Get-Item $logPath
    Write-Host "? Log file: $logPath" -ForegroundColor Green
    Write-Host "   Size: $($logInfo.Length) bytes, Modified: $($logInfo.LastWriteTime.ToString('HH:mm:ss'))" -ForegroundColor Gray
    
    if ($OpenLogs) {
        Write-Host "`nOpening log file..." -ForegroundColor Yellow
        notepad $logPath
    } else {
        Write-Host "`nLast 5 log entries:" -ForegroundColor Cyan
        Get-Content $logPath -Tail 5 | ForEach-Object {
            Write-Host "   $_" -ForegroundColor Gray
        }
    }
} else {
    Write-Host "??  Log file: Not created" -ForegroundColor Yellow
}

# Final verdict
Write-Host ""
if ($success) {
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "  SUCCESS: App runs from C:\temp" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
} else {
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "  FAILED: App does not run from C:\temp" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    
    Write-Host "`nTroubleshooting steps:" -ForegroundColor Yellow
    Write-Host "1. Check for error dialogs that appeared" -ForegroundColor White
    Write-Host "2. Review log file: $logPath" -ForegroundColor White
    Write-Host "3. Check Event Viewer: Application log" -ForegroundColor White
    Write-Host "4. Run with -OpenLogs to view full log" -ForegroundColor White
    Write-Host "5. Check antivirus/security software" -ForegroundColor White
    Write-Host "6. See RUNNING_FROM_DIFFERENT_LOCATIONS.md for details" -ForegroundColor White
}

Write-Host ""
