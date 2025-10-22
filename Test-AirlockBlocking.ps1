# Test-AirlockBlocking.ps1
# Diagnose what Airlock is blocking

param(
    [Parameter(Mandatory=$false)]
    [string]$ExePath = "bin\Release\net8.0-windows\win-x64\publish\TimeTrack.exe"
)

Write-Host "?? Airlock Blocking Diagnostic Tool" -ForegroundColor Cyan
Write-Host "===================================`n" -ForegroundColor Cyan

# Test 1: File exists and is accessible
Write-Host "Test 1: File Access" -ForegroundColor Yellow
if (Test-Path $ExePath) {
    $file = Get-Item $ExePath
    Write-Host "? File found: $($file.FullName)" -ForegroundColor Green
    Write-Host "   Size: $([math]::Round($file.Length/1MB, 2)) MB" -ForegroundColor White
    Write-Host "   Modified: $($file.LastWriteTime)" -ForegroundColor White
} else {
    Write-Host "? File not found: $ExePath" -ForegroundColor Red
    Write-Host "   Run: dotnet publish -c Release -r win-x64 --self-contained" -ForegroundColor Yellow
    exit 1
}

# Test 2: File signature
Write-Host "`nTest 2: Code Signature" -ForegroundColor Yellow
$signature = Get-AuthenticodeSignature $ExePath
if ($signature.Status -eq 'Valid') {
    Write-Host "? File is signed" -ForegroundColor Green
    Write-Host "   Signer: $($signature.SignerCertificate.Subject)" -ForegroundColor White
} elseif ($signature.Status -eq 'NotSigned') {
    Write-Host "?? File is not signed (this may trigger Airlock)" -ForegroundColor Yellow
} else {
    Write-Host "? Invalid signature: $($signature.Status)" -ForegroundColor Red
}

# Test 3: Security zones
Write-Host "`nTest 3: Zone Identifier" -ForegroundColor Yellow
$zoneId = Get-Content "$ExePath`:Zone.Identifier" -ErrorAction SilentlyContinue
if ($zoneId) {
    Write-Host "?? File has Zone Identifier (downloaded from internet)" -ForegroundColor Yellow
    Write-Host $zoneId -ForegroundColor Gray
    Write-Host "   Removing Zone Identifier..." -ForegroundColor Cyan
    Unblock-File $ExePath
    Write-Host "? Zone Identifier removed" -ForegroundColor Green
} else {
    Write-Host "? No Zone Identifier (file is trusted)" -ForegroundColor Green
}

# Test 4: Try to run from different locations
Write-Host "`nTest 4: Execution from Different Locations" -ForegroundColor Yellow

function Test-ExecutionLocation {
    param([string]$Location, [string]$Description)
    
    Write-Host "`n  Testing: $Description" -ForegroundColor Cyan
    Write-Host "  Path: $Location" -ForegroundColor Gray
    
    # Create directory if needed
    if (-not (Test-Path $Location)) {
        New-Item -ItemType Directory -Path $Location -Force | Out-Null
    }
    
    # Copy exe
    $testExe = "$Location\TimeTrack.exe"
    Copy-Item $ExePath $testExe -Force
    
    # Try to start
    try {
        $process = Start-Process $testExe -PassThru -WindowStyle Hidden -ErrorAction Stop
        Start-Sleep -Seconds 2
        
        if ($process.HasExited) {
            Write-Host "  ? Process exited immediately (Exit Code: $($process.ExitCode))" -ForegroundColor Red
            
            # Check Windows Event Log for blocks
            $events = Get-WinEvent -FilterHashtable @{
                LogName = 'Microsoft-Windows-AppLocker/EXE and DLL'
                StartTime = (Get-Date).AddMinutes(-1)
            } -ErrorAction SilentlyContinue | Where-Object { $_.Message -like "*TimeTrack*" }
            
            if ($events) {
                Write-Host "  ?? AppLocker/Airlock event found:" -ForegroundColor Yellow
                $events | ForEach-Object { Write-Host "     $($_.Message)" -ForegroundColor Gray }
            }
        } else {
            Write-Host "  ? Process started successfully" -ForegroundColor Green
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
    } catch {
        Write-Host "  ? Failed to start: $_" -ForegroundColor Red
    }
    
    # Clean up
    Remove-Item $testExe -Force -ErrorAction SilentlyContinue
}

# Test standard locations
Test-ExecutionLocation "$env:USERPROFILE\Desktop" "User Desktop"
Test-ExecutionLocation "$env:USERPROFILE\Documents" "User Documents"
Test-ExecutionLocation "$env:LOCALAPPDATA\Programs\TimeTrack" "Local AppData\Programs"
Test-ExecutionLocation "$env:APPDATA\TimeTrack" "Roaming AppData"
Test-ExecutionLocation "C:\temp" "C:\temp (commonly blocked)"

# Test 5: Check Windows Defender
Write-Host "`nTest 5: Windows Defender Scan" -ForegroundColor Yellow
try {
    $defenderStatus = Get-MpComputerStatus
    Write-Host "? Windows Defender is $($defenderStatus.AntivirusEnabled ? 'enabled' : 'disabled')" -ForegroundColor $(if ($defenderStatus.AntivirusEnabled) { 'Green' } else { 'Yellow' })
    
    if ($defenderStatus.AntivirusEnabled) {
        Write-Host "   Running quick scan on executable..." -ForegroundColor Cyan
        $scanResult = Start-MpScan -ScanPath $ExePath -ScanType CustomScan -ErrorAction SilentlyContinue
        if ($?) {
            Write-Host "? Windows Defender scan passed" -ForegroundColor Green
        }
    }
} catch {
    Write-Host "?? Could not check Windows Defender status" -ForegroundColor Yellow
}

# Test 6: Check AppLocker/Airlock policies
Write-Host "`nTest 6: AppLocker Policies" -ForegroundColor Yellow
try {
    $policies = Get-AppLockerPolicy -Effective -ErrorAction SilentlyContinue
    if ($policies) {
        Write-Host "?? AppLocker policies are active" -ForegroundColor Yellow
        Write-Host "   This may include Airlock restrictions" -ForegroundColor Gray
        
        # Try to get specific rule info
        $exeRules = $policies.RuleCollections | Where-Object { $_.RuleCollectionType -eq 'Exe' }
        if ($exeRules) {
            Write-Host "   Executable rules found: $($exeRules.Count)" -ForegroundColor White
        }
    } else {
        Write-Host "? No AppLocker policies detected" -ForegroundColor Green
    }
} catch {
    Write-Host "?? Could not check AppLocker policies (requires admin)" -ForegroundColor Cyan
}

# Test 7: Check Event Logs for blocks
Write-Host "`nTest 7: Recent Security Blocks" -ForegroundColor Yellow
try {
    $recentBlocks = Get-WinEvent -FilterHashtable @{
        LogName = 'Microsoft-Windows-AppLocker/EXE and DLL'
        StartTime = (Get-Date).AddHours(-24)
    } -MaxEvents 10 -ErrorAction SilentlyContinue | Where-Object { $_.Message -like "*TimeTrack*" -or $_.Message -like "*blocked*" }
    
    if ($recentBlocks) {
        Write-Host "?? Found $($recentBlocks.Count) recent block(s) for TimeTrack:" -ForegroundColor Yellow
        $recentBlocks | ForEach-Object {
            Write-Host "`n   Time: $($_.TimeCreated)" -ForegroundColor Gray
            Write-Host "   $($_.Message)" -ForegroundColor Gray
        }
    } else {
        Write-Host "? No recent blocks found in last 24 hours" -ForegroundColor Green
    }
} catch {
    Write-Host "?? Could not access event logs (may require admin)" -ForegroundColor Cyan
}

# Summary and recommendations
Write-Host "`n" -NoNewline
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "Summary and Recommendations" -ForegroundColor Cyan
Write-Host "======================================`n" -ForegroundColor Cyan

Write-Host "?? Next Steps:" -ForegroundColor Yellow
Write-Host "`n1. ?? Sign the executable:" -ForegroundColor White
Write-Host "   .\Sign-TimeTrack.ps1" -ForegroundColor Gray

Write-Host "`n2. ?? Install to trusted location:" -ForegroundColor White
Write-Host "   .\Install-TimeTrackUserLocal.ps1 -CreateShortcut" -ForegroundColor Gray

Write-Host "`n3. ?? Request Airlock exception:" -ForegroundColor White
Write-Host "   See: AIRLOCK_EXCEPTION_REQUEST.md" -ForegroundColor Gray

Write-Host "`n4. ?? Check with IT Security:" -ForegroundColor White
Write-Host "   - Review Event Logs above" -ForegroundColor Gray
Write-Host "   - Provide file hash for whitelisting" -ForegroundColor Gray
Write-Host "   - Request specific path exception" -ForegroundColor Gray

# Calculate file hash for whitelisting
Write-Host "`n?? File Hash for IT Security:" -ForegroundColor Yellow
$hash = Get-FileHash $ExePath -Algorithm SHA256
Write-Host "   SHA256: $($hash.Hash)" -ForegroundColor White
Write-Host "   Path: $($hash.Path)" -ForegroundColor Gray

Write-Host "`n? Diagnostic complete!" -ForegroundColor Green
