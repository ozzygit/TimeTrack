# Diagnose-Installation.ps1
# Run this ON THE WORK PC to diagnose why it's asking for .NET Runtime
# Can be run from anywhere - just point it to the TimeTrack folder

param(
    [string]$TimeTrackPath = "C:\temp\win-x64"
)

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  TimeTrack Installation Diagnostic" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

if (-not (Test-Path $TimeTrackPath)) {
    Write-Host "? Path not found: $TimeTrackPath" -ForegroundColor Red
    $TimeTrackPath = Read-Host "Enter the path where you copied TimeTrack"
    
if (-not (Test-Path $TimeTrackPath)) {
        Write-Host "? Path still not found. Exiting." -ForegroundColor Red
     exit 1
    }
}

Write-Host "Checking: $TimeTrackPath`n" -ForegroundColor Gray

# Check 1: Is the EXE there?
Write-Host "??? Checking Files ???" -ForegroundColor Yellow
$exePath = Join-Path $TimeTrackPath "TimeTrackv2.exe"

if (-not (Test-Path $exePath)) {
    Write-Host "? TimeTrackv2.exe not found!" -ForegroundColor Red
    Write-Host "Expected at: $exePath" -ForegroundColor Gray
    exit 1
}

$exe = Get-Item $exePath
Write-Host "? TimeTrackv2.exe found ($([math]::Round($exe.Length / 1KB, 1)) KB)" -ForegroundColor Green

# Check 2: Critical runtime files
$criticalFiles = @{
    "hostfxr.dll" = "Self-contained host"
    "hostpolicy.dll" = "Self-contained policy"
    "coreclr.dll" = ".NET Core runtime"
    "System.Private.CoreLib.dll" = "Core library"
    "TimeTrackv2.dll" = "Main application"
    "TimeTrackv2.runtimeconfig.json" = "Runtime config"
}

Write-Host "`nCritical Files Check:" -ForegroundColor White
$missingCritical = @()

foreach ($file in $criticalFiles.Keys) {
    $filePath = Join-Path $TimeTrackPath $file
    $description = $criticalFiles[$file]
    
    if (Test-Path $filePath) {
        $fileInfo = Get-Item $filePath
        Write-Host "  ? $file ($description)" -ForegroundColor Green
    } else {
Write-Host "  ? $file - MISSING ($description)" -ForegroundColor Red
        $missingCritical += $file
    }
}

# Check 3: File count
Write-Host "`n??? File Statistics ???" -ForegroundColor Yellow
$allFiles = Get-ChildItem $TimeTrackPath -File
$dllFiles = $allFiles | Where-Object { $_.Extension -eq ".dll" }
$totalSize = ($allFiles | Measure-Object -Property Length -Sum).Sum

Write-Host "Total files: $($allFiles.Count)" -ForegroundColor Gray
Write-Host "DLL files: $($dllFiles.Count)" -ForegroundColor Gray
Write-Host "Total size: $([math]::Round($totalSize / 1MB, 1)) MB" -ForegroundColor Gray

if ($allFiles.Count -lt 200) {
    Write-Host "`n? WARNING: Only $($allFiles.Count) files found!" -ForegroundColor Yellow
    Write-Host "  Expected ~260 files for self-contained build" -ForegroundColor Yellow
    Write-Host "  This looks like an INCOMPLETE copy!" -ForegroundColor Red
}

if ($totalSize -lt 100MB) {
    Write-Host "`n? WARNING: Total size is only $([math]::Round($totalSize / 1MB, 1)) MB" -ForegroundColor Yellow
Write-Host "  Expected ~150 MB for self-contained build" -ForegroundColor Yellow
    Write-Host "  This might be a framework-dependent build!" -ForegroundColor Red
}

# Check 4: Runtime config analysis
Write-Host "`n??? Runtime Configuration ???" -ForegroundColor Yellow
$runtimeConfigPath = Join-Path $TimeTrackPath "TimeTrackv2.runtimeconfig.json"

if (Test-Path $runtimeConfigPath) {
    try {
        $rcContent = Get-Content $runtimeConfigPath -Raw
        $rc = $rcContent | ConvertFrom-Json
        
        Write-Host "Config file found:" -ForegroundColor Green
        Write-Host $rcContent -ForegroundColor Gray
        
        if ($rc.runtimeOptions.includedFrameworks) {
         Write-Host "`n? includedFrameworks found - This IS self-contained" -ForegroundColor Green
     Write-Host "Included frameworks:" -ForegroundColor White
            foreach ($fw in $rc.runtimeOptions.includedFrameworks) {
     Write-Host "  - $($fw.name) $($fw.version)" -ForegroundColor Gray
    }
        } else {
    Write-Host "`n? includedFrameworks NOT found - This is FRAMEWORK-DEPENDENT" -ForegroundColor Red
        }
        
        if ($rc.runtimeOptions.framework) {
       Write-Host "`n? 'framework' property found (framework-dependent indicator)" -ForegroundColor Yellow
 Write-Host "  Framework: $($rc.runtimeOptions.framework.name) $($rc.runtimeOptions.framework.version)" -ForegroundColor Gray
        }
    } catch {
      Write-Host "? Could not parse runtimeconfig.json: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "? runtimeconfig.json not found" -ForegroundColor Red
}

# Check 5: File properties and timestamps
Write-Host "`n??? File Properties ???" -ForegroundColor Yellow
Write-Host "EXE creation time: $($exe.CreationTime)" -ForegroundColor Gray
Write-Host "EXE modified time: $($exe.LastWriteTime)" -ForegroundColor Gray

# Check if running from USB
$drive = Split-Path $TimeTrackPath -Qualifier
if ($drive) {
  try {
      $vol = Get-Volume -DriveLetter $drive.TrimEnd(':')
        if ($vol.DriveType -eq 'Removable') {
        Write-Host "`n? WARNING: You are running from a REMOVABLE DRIVE!" -ForegroundColor Red
 Write-Host "  Drive: $drive ($($vol.FileSystemLabel))" -ForegroundColor Yellow
        Write-Host "  This may cause the .NET Runtime prompt due to security restrictions!" -ForegroundColor Yellow
       Write-Host "`n  SOLUTION: Copy to C:\Apps\TimeTrack and run from there" -ForegroundColor White
        } else {
          Write-Host "`nDrive type: $($vol.DriveType)" -ForegroundColor Green
        }
    } catch {
    Write-Host "`nCould not determine drive type" -ForegroundColor Gray
    }
}

# Check 6: Security/Antivirus blocks
Write-Host "`n??? Security Check ???" -ForegroundColor Yellow

# Check for Zone.Identifier (file downloaded from internet)
$zoneIdPath = "$exePath`:Zone.Identifier"
if (Test-Path $zoneIdPath -PathType Leaf) {
    Write-Host "? File is marked as 'Downloaded from Internet'" -ForegroundColor Yellow
    Write-Host "  This may cause SmartScreen or security blocks" -ForegroundColor Yellow
    Write-Host "`n  SOLUTION: Right-click the folder ? Properties ? Unblock" -ForegroundColor White
} else {
    Write-Host "? File is not marked as downloaded" -ForegroundColor Green
}

# Check 7: Suggest what to try
Write-Host "`n??? DIAGNOSIS SUMMARY ???" -ForegroundColor Cyan

if ($missingCritical.Count -gt 0) {
    Write-Host "`n? PROBLEM: Critical files are MISSING" -ForegroundColor Red
    Write-Host "Missing files:" -ForegroundColor Yellow
    $missingCritical | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    Write-Host "`nSOLUTION:" -ForegroundColor White
    Write-Host "  1. Delete this folder" -ForegroundColor White
    Write-Host "  2. Re-copy ALL files from: bin\publish\win-x64" -ForegroundColor White
    Write-Host "  3. Make sure you copy the ENTIRE folder, not just the EXE" -ForegroundColor White
}
elseif ($allFiles.Count -lt 200 -or $totalSize -lt 100MB) {
    Write-Host "`n? PROBLEM: Incomplete installation detected" -ForegroundColor Red
    Write-Host "  Only $($allFiles.Count) files / $([math]::Round($totalSize / 1MB, 1)) MB" -ForegroundColor Yellow
  Write-Host "`nSOLUTION:" -ForegroundColor White
    Write-Host "  1. Delete this folder" -ForegroundColor White
  Write-Host "  2. On dev PC, run: dotnet publish -c Release" -ForegroundColor White
    Write-Host "  3. Copy the ENTIRE folder: bin\publish\win-x64" -ForegroundColor White
    Write-Host "  4. Should have ~260 files / ~150 MB" -ForegroundColor White
}
elseif ($drive -and (Get-Volume -DriveLetter $drive.TrimEnd(':')).DriveType -eq 'Removable') {
    Write-Host "`n? PROBLEM: Running from USB/Removable drive" -ForegroundColor Red
    Write-Host "`nSOLUTION:" -ForegroundColor White
    Write-Host "  Copy this folder to: C:\Apps\TimeTrack" -ForegroundColor White
    Write-Host "  Then run from there" -ForegroundColor White
}
else {
    Write-Host "`n? Installation looks correct!" -ForegroundColor Green
    Write-Host "`nIf you're still getting the .NET Runtime prompt:" -ForegroundColor Yellow
    Write-Host "  1. Check if antivirus is blocking files" -ForegroundColor White
    Write-Host "  2. Try running as Administrator" -ForegroundColor White
    Write-Host "  3. Check Windows Event Viewer for errors" -ForegroundColor White
    Write-Host "  4. Unblock the folder (Properties ? Unblock)" -ForegroundColor White
    Write-Host "  5. Try running from a different location (e.g., C:\Apps\TimeTrack)" -ForegroundColor White
}

Write-Host "`n??? Additional Info ???" -ForegroundColor Cyan
Write-Host "Full path being checked: $TimeTrackPath" -ForegroundColor Gray
Write-Host "Run this script again after making changes to verify" -ForegroundColor Gray
Write-Host ""

# Offer to create a proper copy
$response = Read-Host "`nWould you like to copy this to C:\Apps\TimeTrack now? (y/n)"
if ($response -eq 'y') {
    $destPath = "C:\Apps\TimeTrack"
    
    if (Test-Path $destPath) {
        $response2 = Read-Host "C:\Apps\TimeTrack already exists. Overwrite? (y/n)"
        if ($response2 -ne 'y') {
            Write-Host "Cancelled." -ForegroundColor Gray
            exit 0
        }
        Remove-Item $destPath -Recurse -Force
    }
    
    try {
        Write-Host "Copying files..." -ForegroundColor Yellow
    Copy-Item $TimeTrackPath -Destination $destPath -Recurse -Force
      Write-Host "? Copied to: $destPath" -ForegroundColor Green
        Write-Host "`nTry running: $destPath\TimeTrackv2.exe" -ForegroundColor White
    } catch {
        Write-Host "? Copy failed: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "You may need administrator privileges" -ForegroundColor Yellow
    }
}
