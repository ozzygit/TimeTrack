# Diagnose-WorkPC.ps1
# Run this ON YOUR WORK PC in the same folder as TimeTrackv2.exe

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Work PC Runtime Detection Diagnostic" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$exeName = "TimeTrackv2.exe"
$currentDir = Get-Location

Write-Host "Current folder: $currentDir" -ForegroundColor Gray
Write-Host ""

# 1. Verify we're in the right folder
Write-Host "[1/6] Verifying application files..." -ForegroundColor Yellow
if (-not (Test-Path $exeName)) {
    Write-Host "[ERROR] $exeName not found in current directory" -ForegroundColor Red
    Write-Host "Navigate to the folder containing TimeTrackv2.exe and run this script again" -ForegroundColor Yellow
    exit 1
}
Write-Host "[OK] Found $exeName" -ForegroundColor Green

# 2. Check for runtime DLLs
Write-Host "`n[2/6] Checking runtime DLLs..." -ForegroundColor Yellow
$runtimeDlls = @("coreclr.dll", "hostfxr.dll", "hostpolicy.dll", "System.Private.CoreLib.dll", "mscorlib.dll")
$missingDlls = @()

foreach ($dll in $runtimeDlls) {
    $exists = Test-Path $dll
    $status = if ($exists) { "[OK]" } else { "[MISSING]" }
    $color = if ($exists) { "Green" } else { "Red" }
    Write-Host "$status $dll" -ForegroundColor $color
    if (-not $exists) { $missingDlls += $dll }
}

# 3. Check runtimeconfig.json
Write-Host "`n[3/6] Checking runtimeconfig.json..." -ForegroundColor Yellow
$rcPath = "TimeTrackv2.runtimeconfig.json"
if (Test-Path $rcPath) {
    Write-Host "[OK] Found runtimeconfig.json" -ForegroundColor Green
    try {
        $rc = Get-Content $rcPath -Raw | ConvertFrom-Json
        Write-Host "`nContents:" -ForegroundColor Gray
        $rc | ConvertTo-Json -Depth 5 | Write-Host -ForegroundColor DarkGray
  
        if ($rc.runtimeOptions.includedFrameworks) {
         Write-Host "`n[OK] includedFrameworks present (self-contained)" -ForegroundColor Green
            $rc.runtimeOptions.includedFrameworks | ForEach-Object {
       Write-Host "  - $($_.name) $($_.version)" -ForegroundColor Gray
            }
        } else {
        Write-Host "`n[PROBLEM] No includedFrameworks found!" -ForegroundColor Red
   Write-Host "This indicates a framework-dependent build" -ForegroundColor Yellow
        }
        
        if ($rc.runtimeOptions.tfm) {
 Write-Host "`nTarget Framework: $($rc.runtimeOptions.tfm)" -ForegroundColor Gray
        }
    } catch {
        Write-Host "[ERROR] Failed to parse runtimeconfig.json: $_" -ForegroundColor Red
    }
} else {
    Write-Host "[MISSING] runtimeconfig.json not found" -ForegroundColor Red
}

# 4. Check deps.json
Write-Host "`n[4/6] Checking deps.json..." -ForegroundColor Yellow
$depsPath = "TimeTrackv2.deps.json"
if (Test-Path $depsPath) {
    Write-Host "[OK] Found deps.json" -ForegroundColor Green
    $depsSize = (Get-Item $depsPath).Length / 1KB
    Write-Host "Size: $([math]::Round($depsSize, 2)) KB" -ForegroundColor Gray
} else {
    Write-Host "[MISSING] deps.json not found" -ForegroundColor Red
}

# 5. Check total folder size
Write-Host "`n[5/6] Checking total application size..." -ForegroundColor Yellow
$totalSize = (Get-ChildItem -File | Measure-Object -Property Length -Sum).Sum / 1MB
Write-Host "Total size: $([math]::Round($totalSize, 2)) MB" -ForegroundColor Cyan

if ($totalSize -gt 100) {
    Write-Host "[OK] Size indicates self-contained (~140-180 MB)" -ForegroundColor Green
} elseif ($totalSize -gt 50) {
    Write-Host "[WARN] Size is in between (50-100 MB)" -ForegroundColor Yellow
} else {
    Write-Host "[PROBLEM] Size too small (<50 MB) - likely framework-dependent" -ForegroundColor Red
}

# 6. Try to get .NET runtime info from the exe
Write-Host "`n[6/6] Checking executable metadata..." -ForegroundColor Yellow
try {
  $exe = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exeName)
    Write-Host "[OK] File version: $($exe.FileVersion)" -ForegroundColor Green
    Write-Host "Product version: $($exe.ProductVersion)" -ForegroundColor Gray
    Write-Host "Company: $($exe.CompanyName)" -ForegroundColor Gray
} catch {
  Write-Host "[WARN] Could not read file version info" -ForegroundColor Yellow
}

# Check if exe has embedded runtime (PublishSingleFile)
$exeSize = (Get-Item $exeName).Length / 1MB
Write-Host "Executable size: $([math]::Round($exeSize, 2)) MB" -ForegroundColor Cyan

if ($exeSize -gt 100) {
    Write-Host "[INFO] Large exe size suggests PublishSingleFile=true" -ForegroundColor Cyan
    Write-Host "Runtime is embedded in the executable" -ForegroundColor Gray
} else {
    Write-Host "[INFO] Small exe size suggests multi-file publish" -ForegroundColor Cyan
    Write-Host "Runtime should be in separate DLLs" -ForegroundColor Gray
}

# Summary
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Summary & Diagnosis" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$problems = @()

if ($missingDlls.Count -gt 0) {
    $problems += "Missing runtime DLLs: $($missingDlls -join ', ')"
}

if (-not (Test-Path $rcPath)) {
    $problems += "Missing runtimeconfig.json"
} elseif ($rc -and -not $rc.runtimeOptions.includedFrameworks) {
    $problems += "runtimeconfig.json indicates framework-dependent build"
}

if ($totalSize -lt 50) {
    $problems += "Application size too small for self-contained build"
}

if ($problems.Count -eq 0) {
    Write-Host "[SUCCESS] Everything looks correct!" -ForegroundColor Green
    Write-Host "`nThe application should NOT prompt for .NET Desktop Runtime." -ForegroundColor White
    Write-Host "`nIf it still does, possible causes:" -ForegroundColor Yellow
    Write-Host "1. Antivirus/Airlock quarantined runtime DLLs after copy" -ForegroundColor White
    Write-Host "2. Running wrong executable (check path)" -ForegroundColor White
    Write-Host "3. Windows showing cached installer prompt" -ForegroundColor White
    Write-Host "`nTry:" -ForegroundColor Cyan
 Write-Host "- Restart Windows" -ForegroundColor White
    Write-Host "- Re-extract from original ZIP to fresh folder" -ForegroundColor White
 Write-Host "- Run: .\TimeTrackv2.exe (from this directory)" -ForegroundColor White
} else {
    Write-Host "[PROBLEMS DETECTED]" -ForegroundColor Red
    $problems | ForEach-Object { Write-Host "- $_" -ForegroundColor Yellow }
    
    Write-Host "`nLikely cause:" -ForegroundColor Cyan
    if ($missingDlls.Count -gt 0) {
        Write-Host "Runtime DLLs are missing. This could be:" -ForegroundColor White
        Write-Host "- Incomplete extraction from ZIP" -ForegroundColor White
        Write-Host "- Antivirus deleted the DLLs" -ForegroundColor White
        Write-Host "- Wrong folder copied" -ForegroundColor White
    } else {
   Write-Host "The build appears to be framework-dependent, not self-contained." -ForegroundColor White
        Write-Host "This would require .NET Desktop Runtime to be installed." -ForegroundColor White
    }
    
    Write-Host "`nRecommended action:" -ForegroundColor Cyan
    Write-Host "1. On DEV PC, verify publish is self-contained:" -ForegroundColor White
 Write-Host "   .\Diagnostic-PublishFolder.ps1" -ForegroundColor Gray
  Write-Host "2. Create fresh deployment package:" -ForegroundColor White
    Write-Host "   .\Deploy-ToWorkPC.ps1" -ForegroundColor Gray
    Write-Host "3. Transfer ZIP to work PC" -ForegroundColor White
    Write-Host "4. Extract to FRESH folder (delete old if exists)" -ForegroundColor White
    Write-Host "5. Run this diagnostic again" -ForegroundColor White
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Current folder contents (first 20 files):" -ForegroundColor Yellow
Get-ChildItem -File | Select-Object Name, @{N="Size (KB)";E={[math]::Round($_.Length/1KB,2)}} | Select-Object -First 20 | Format-Table -AutoSize

Write-Host ""
