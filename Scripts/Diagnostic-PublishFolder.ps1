# Diagnostic-PublishFolder.ps1
# Check what's actually in your publish folder

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

Write-Host "Project root: $projectRoot" -ForegroundColor Gray

$publishDir = Join-Path $projectRoot "bin\Release\net8.0-windows\win-x64\publish"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  TimeTrack Publish Folder Diagnostic" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Check if publish folder exists
if (-not (Test-Path $publishDir)) {
    Write-Host "ERROR: Publish folder not found at: $publishDir" -ForegroundColor Red
    Write-Host "Run: dotnet publish -c Release -r win-x64 --self-contained" -ForegroundColor Yellow
    exit 1
}

# 1. Check runtime DLLs
Write-Host "[1/4] Checking for runtime DLLs..." -ForegroundColor Yellow
$requiredDlls = @("coreclr.dll", "hostfxr.dll", "hostpolicy.dll", "System.Private.CoreLib.dll")
$missing = @()

foreach ($dll in $requiredDlls) {
    $exists = Test-Path (Join-Path $publishDir $dll)
    $status = if ($exists) { "[OK]" } else { "[MISSING]" }
  $color = if ($exists) { "Green" } else { "Red" }
    Write-Host "$status $dll" -ForegroundColor $color
    if (-not $exists) { $missing += $dll }
}

# 2. Check runtimeconfig.json
Write-Host "`n[2/4] Checking runtimeconfig.json..." -ForegroundColor Yellow
$rcPath = Join-Path $publishDir "TimeTrackv2.runtimeconfig.json"
if (Test-Path $rcPath) {
    $rc = Get-Content $rcPath -Raw | ConvertFrom-Json
    if ($rc.runtimeOptions.includedFrameworks) {
        Write-Host "[OK] Self-contained (includedFrameworks present)" -ForegroundColor Green
        $rc.runtimeOptions.includedFrameworks | ForEach-Object { 
   Write-Host "  - $($_.name) $($_.version)" -ForegroundColor Gray 
        }
    } else {
        Write-Host "[FAIL] Framework-dependent (no includedFrameworks)" -ForegroundColor Red
        Write-Host "  This will prompt for .NET Desktop Runtime on machines without it!" -ForegroundColor Yellow
    }
} else {
    Write-Host "[FAIL] runtimeconfig.json not found" -ForegroundColor Red
    $missing += "TimeTrackv2.runtimeconfig.json"
}

# 3. Measure total size
Write-Host "`n[3/4] Checking publish size..." -ForegroundColor Yellow
$totalSize = (Get-ChildItem $publishDir -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB
Write-Host "Total publish size: $([math]::Round($totalSize, 2)) MB" -ForegroundColor Cyan

if ($totalSize -gt 100) {
    Write-Host "[OK] Size indicates self-contained build (140-180 MB typical)" -ForegroundColor Green
} else {
    Write-Host "[WARN] Size indicates framework-dependent build (5-20 MB typical)" -ForegroundColor Yellow
}

# 4. List key files
Write-Host "`n[4/4] Key files in publish folder:" -ForegroundColor Yellow
Get-ChildItem $publishDir -Filter "TimeTrackv2.*" | Select-Object Name, @{N="Size (KB)";E={[math]::Round($_.Length/1KB,2)}}
Get-ChildItem $publishDir -Filter "host*.*" -ErrorAction SilentlyContinue | Select-Object Name, @{N="Size (KB)";E={[math]::Round($_.Length/1KB,2)}}
Get-ChildItem $publishDir -Filter "coreclr.dll" -ErrorAction SilentlyContinue | Select-Object Name, @{N="Size (KB)";E={[math]::Round($_.Length/1KB,2)}}

# Summary
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Summary" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

if ($missing.Count -eq 0 -and $totalSize -gt 100) {
    Write-Host "[SUCCESS] Publish folder looks self-contained!" -ForegroundColor Green
    Write-Host "`nNext steps:" -ForegroundColor Yellow
    Write-Host "1. Copy THE ENTIRE contents of $publishDir to your work PC" -ForegroundColor White
    Write-Host "2. Consider zipping first to avoid Airlock interference:" -ForegroundColor White
    Write-Host "   Compress-Archive -Path '$publishDir\*' -DestinationPath 'TimeTrack-Deploy.zip'" -ForegroundColor Gray
    Write-Host "3. Extract and run on work PC" -ForegroundColor White
} else {
    Write-Host "[PROBLEM DETECTED] Publish is NOT self-contained!" -ForegroundColor Red
    Write-Host "`nIssues found:" -ForegroundColor Yellow
    if ($missing.Count -gt 0) {
        Write-Host "  - Missing files: $($missing -join ', ')" -ForegroundColor Red
    }
    if ($totalSize -lt 100) {
        Write-Host "  - Build size too small (framework-dependent)" -ForegroundColor Red
    }
    Write-Host "`nFix by running:" -ForegroundColor Yellow
    Write-Host "  cd '$projectRoot'" -ForegroundColor Gray
    Write-Host "  Remove-Item -Path 'bin','obj' -Recurse -Force -ErrorAction SilentlyContinue" -ForegroundColor Gray
 Write-Host "  dotnet publish -c Release -r win-x64 -p:SelfContained=true --self-contained true" -ForegroundColor Gray
}

Write-Host ""
