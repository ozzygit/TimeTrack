# Test-TimeTrackFromTemp.ps1
# Tests TimeTrack execution from c:\temp with diagnostics

Write-Host "=== TimeTrack c:\temp Test ===" -ForegroundColor Cyan

# Ensure c:\temp exists
if (-not (Test-Path "C:\temp")) {
    Write-Host "Creating C:\temp directory..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path "C:\temp" -Force | Out-Null
}

# Copy published executable
$publishPath = "bin\Release\net8.0-windows\win-x64\publish\TimeTrack.exe"
if (-not (Test-Path $publishPath)) {
    Write-Host "? Published executable not found at: $publishPath" -ForegroundColor Red
    Write-Host "Run: dotnet publish -c Release -r win-x64 --self-contained" -ForegroundColor Yellow
    exit 1
}

Write-Host "Copying executable to C:\temp..." -ForegroundColor Yellow
Copy-Item $publishPath "C:\temp\" -Force

# Check file properties
$file = Get-Item "C:\temp\TimeTrack.exe"
Write-Host "? File copied: $($file.Length) bytes" -ForegroundColor Green

# Check permissions
Write-Host "`nChecking C:\temp permissions..." -ForegroundColor Yellow
try {
    $acl = Get-Acl "C:\temp"
    Write-Host "? Can read permissions" -ForegroundColor Green
    
    # Test write access
    $testFile = "C:\temp\timetrack_test_$([guid]::NewGuid()).txt"
    "test" | Out-File $testFile
    Remove-Item $testFile -Force
    Write-Host "? Can write to C:\temp" -ForegroundColor Green
} catch {
    Write-Host "? Permission issue: $($_.Exception.Message)" -ForegroundColor Red
}

# Try to run
Write-Host "`nAttempting to run TimeTrack.exe from c:\temp..." -ForegroundColor Yellow
Write-Host "Watch for error dialogs. Press Ctrl+C to stop monitoring.`n" -ForegroundColor Gray

try {
    $process = Start-Process -FilePath "C:\temp\TimeTrack.exe" -PassThru -ErrorAction Stop
    
    # Monitor for 10 seconds
    for ($i = 0; $i -lt 20; $i++) {
        Start-Sleep -Milliseconds 500
        
        if ($process.HasExited) {
            Write-Host "`n? Process exited with code: $($process.ExitCode)" -ForegroundColor Red
            
            if ($process.ExitCode -eq -1) {
                Write-Host "   This usually indicates a crash or unhandled exception" -ForegroundColor Yellow
            } elseif ($process.ExitCode -eq 1) {
                Write-Host "   The application encountered a startup error (see error dialog)" -ForegroundColor Yellow
            }
            
            break
        }
        
        Write-Host "." -NoNewline -ForegroundColor Green
    }
    
    Write-Host ""
    
    if (-not $process.HasExited) {
        Write-Host "? Application is running!" -ForegroundColor Green
        Write-Host "Stopping test process..." -ForegroundColor Yellow
        Stop-Process -Id $process.Id -Force
    }
} catch {
    Write-Host "? Failed to start process: $($_.Exception.Message)" -ForegroundColor Red
}

# Check database creation
$dbPath = "$env:APPDATA\TimeTrack v2\timetrack_v2.db"
if (Test-Path $dbPath) {
    Write-Host "`n? Database exists at: $dbPath" -ForegroundColor Green
    $dbFile = Get-Item $dbPath
    Write-Host "   Size: $($dbFile.Length) bytes" -ForegroundColor Gray
    Write-Host "   Modified: $($dbFile.LastWriteTime)" -ForegroundColor Gray
} else {
    Write-Host "`n??  Database not found at: $dbPath" -ForegroundColor Yellow
}

# Check log file
$logPath = "$env:LOCALAPPDATA\TimeTrack v2\time_track_log.txt"
if (Test-Path $logPath) {
    Write-Host "`nRecent log entries:" -ForegroundColor Cyan
    Get-Content $logPath -Tail 10
} else {
    Write-Host "`n??  No log file found at: $logPath" -ForegroundColor Yellow
}

# Check Windows Event Log for .NET errors
Write-Host "`nChecking Windows Event Log for recent .NET errors..." -ForegroundColor Yellow
try {
    $recentErrors = Get-EventLog -LogName Application -Source ".NET Runtime" -After (Get-Date).AddMinutes(-5) -ErrorAction SilentlyContinue | 
        Where-Object { $_.EntryType -eq "Error" } | 
        Select-Object -First 3
    
    if ($recentErrors) {
        Write-Host "? Found .NET Runtime errors:" -ForegroundColor Red
        $recentErrors | ForEach-Object {
            Write-Host "   Time: $($_.TimeGenerated)" -ForegroundColor Gray
            Write-Host "   Message: $($_.Message.Substring(0, [Math]::Min(200, $_.Message.Length)))..." -ForegroundColor Gray
            Write-Host ""
        }
    } else {
        Write-Host "? No recent .NET Runtime errors" -ForegroundColor Green
    }
} catch {
    Write-Host "??  Could not check Event Log (may require admin)" -ForegroundColor Yellow
}

# Check for extracted native libraries
Write-Host "`nChecking for extracted SQLite libraries..." -ForegroundColor Yellow
$tempFolders = Get-ChildItem $env:TEMP -Directory -Filter ".net" -ErrorAction SilentlyContinue
if ($tempFolders) {
    $sqliteLibs = $tempFolders | ForEach-Object {
        Get-ChildItem $_.FullName -Recurse -Filter "e_sqlite3.dll" -ErrorAction SilentlyContinue
    }
    
    if ($sqliteLibs) {
        Write-Host "? Found SQLite native library:" -ForegroundColor Green
        $sqliteLibs | ForEach-Object {
            Write-Host "   $($_.FullName)" -ForegroundColor Gray
        }
    } else {
        Write-Host "??  SQLite library not found in temp extraction folders" -ForegroundColor Yellow
    }
} else {
    Write-Host "??  No .NET extraction folders found in temp" -ForegroundColor Yellow
}

Write-Host "`n=== Test Complete ===" -ForegroundColor Cyan
Write-Host "If the application failed to start, check:" -ForegroundColor Yellow
Write-Host "1. Error dialog that should have appeared" -ForegroundColor White
Write-Host "2. Log file at: $logPath" -ForegroundColor White
Write-Host "3. Windows Event Viewer (Application log)" -ForegroundColor White
Write-Host "4. Your antivirus/security software logs" -ForegroundColor White
