# Publish-And-Test.ps1
# Complete workflow to publish and test TimeTrack from c:\temp

param(
    [switch]$SkipPublish,
    [switch]$OpenLogs
)

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

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  TimeTrack Publish & Test from c:\temp" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan
Write-Host "Project root: $projectRoot" -ForegroundColor Gray
Write-Host ""

# Paths
$publishDir = Join-Path $projectRoot "bin/Release/net8.0-windows/win-x64/publish"

# Step1: Clean and publish (unless skipped)
if (-not $SkipPublish) {
    Write-Host "[1/6] Cleaning previous build..." -ForegroundColor Yellow
    Push-Location $projectRoot
    dotnet clean --configuration Release
    
    Write-Host "[2/6] Removing previous publish output..." -ForegroundColor Yellow
    if (Test-Path $publishDir) {
        try { Remove-Item -LiteralPath $publishDir -Recurse -Force -ErrorAction Stop } catch { Write-Host "(warn) Could not fully clean publish folder: $($_.Exception.Message)" -ForegroundColor DarkYellow }
    }

    Write-Host "[3/6] Publishing self-contained executable (win-x64)..." -ForegroundColor Yellow
    # Be explicit about self-contained + RID to avoid accidental framework-dependent publish
    dotnet publish -c Release -r win-x64 -p:SelfContained=true -p:PublishSingleFile=false
    Pop-Location

    if ($LASTEXITCODE -ne 0) {
        Write-Host "`n? Publish failed!" -ForegroundColor Red
     exit 1
    }

    Write-Host "? Publish successful`n" -ForegroundColor Green
} else {
    Write-Host "[Skipped] Using existing published executable`n" -ForegroundColor Gray
}

# Step2: Validate publish is self-contained
Write-Host "[4/6] Validating publish output..." -ForegroundColor Yellow
$mustHave = @(
 "hostfxr.dll",
    "hostpolicy.dll",
    "coreclr.dll",
    "System.Private.CoreLib.dll"
)
$missing = @()
foreach ($f in $mustHave) {
    if (-not (Test-Path (Join-Path $publishDir $f))) { $missing += $f }
}

$runtimeConfigPath = Join-Path $publishDir "TimeTrackv2.runtimeconfig.json"
$depsPath = Join-Path $publishDir "TimeTrackv2.deps.json"
$exePath = Join-Path $publishDir "TimeTrackv2.exe"

$hasIncludedFrameworks = $false
if (Test-Path $runtimeConfigPath) {
    try {
      $rc = Get-Content $runtimeConfigPath -Raw | ConvertFrom-Json
    if ($rc.runtimeOptions -and $rc.runtimeOptions.includedFrameworks) { $hasIncludedFrameworks = $true }
    } catch {}
}

if ($missing.Count -gt 0 -or -not $hasIncludedFrameworks) {
    Write-Host "`n? Detected a framework-dependent publish. This will prompt for .NET Desktop Runtime on machines without it." -ForegroundColor Red
    if ($missing.Count -gt 0) { Write-Host ("Missing files: " + ($missing -join ", ")) -ForegroundColor Red }
    if (-not $hasIncludedFrameworks) { Write-Host "'includedFrameworks' not found in runtimeconfig (expected for self-contained)." -ForegroundColor Red }
    Write-Host "Ensure the publish command used '-p:SelfContained=true -r win-x64' and copy the entire publish folder." -ForegroundColor Yellow
    Write-Host "Publish folder: $publishDir" -ForegroundColor Yellow
    exit 2
}

Write-Host "? Publish output looks self-contained." -ForegroundColor Green
Write-Host " ? exe: $exePath" -ForegroundColor Green
Write-Host " ? deps: $depsPath" -ForegroundColor Green
Write-Host " ? runtimeconfig: $runtimeConfigPath" -ForegroundColor Green

# Step3: Basic file listing to help users copy the right items
Write-Host "`n[5/6] Key files in publish folder:" -ForegroundColor Yellow
Get-ChildItem $publishDir -Filter "TimeTrackv2.*" | Select-Object Name, Length
Get-ChildItem $publishDir -Filter "host*.*" | Select-Object Name, Length
Get-ChildItem $publishDir -Filter "coreclr.dll" | Select-Object Name, Length

# Step4: Next steps reminder
Write-Host "`n[6/6] Next steps:" -ForegroundColor Yellow
Write-Host "- Copy EVERYTHING from the publish folder to your target location (e.g., C:\Apps\TimeTrack)" -ForegroundColor Gray
Write-Host "- Run the exe from that same folder: $exePath" -ForegroundColor Gray
Write-Host "- If you still see a runtime prompt, you likely copied only the exe or used a non-publish build." -ForegroundColor Gray

if ($OpenLogs) {
# Optional: open the publish folder in Explorer
    Start-Process explorer.exe $publishDir
}

Write-Host ""

