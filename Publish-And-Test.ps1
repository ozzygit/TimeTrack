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

