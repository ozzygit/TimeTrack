# Verify-SelfContainedPublish.ps1
# Checks if the publish output is truly self-contained

param(
    [string]$PublishPath = "bin\publish\win-x64"
)

# Auto-detect project root
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = $scriptDir

while ($projectRoot -and -not (Get-ChildItem -Path $projectRoot -Filter "*.csproj" -ErrorAction SilentlyContinue)) {
    $parent = Split-Path -Parent $projectRoot
    if ($parent -eq $projectRoot) { break }
    $projectRoot = $parent
}

if (-not (Get-ChildItem -Path $projectRoot -Filter "*.csproj" -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: Could not find project root (.csproj file)" -ForegroundColor Red
    exit 1
}

$fullPublishPath = Join-Path $projectRoot $PublishPath

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Self-Contained Publish Verification" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

if (-not (Test-Path $fullPublishPath)) {
    Write-Host "? Publish folder not found: $fullPublishPath" -ForegroundColor Red
 Write-Host "`nRun one of these commands first:" -ForegroundColor Yellow
    Write-Host "  dotnet publish -c Release" -ForegroundColor White
    Write-Host "  dotnet publish -p:PublishProfile=Win64-SelfContained" -ForegroundColor White
    exit 1
}

Write-Host "Checking: $fullPublishPath`n" -ForegroundColor Gray

# Critical files for self-contained deployment
$criticalFiles = @{
    "TimeTrackv2.exe" = "Main executable"
    "TimeTrackv2.dll" = "Main application DLL"
    "TimeTrackv2.runtimeconfig.json" = "Runtime configuration"
    "hostfxr.dll" = ".NET host (CRITICAL for self-contained)"
    "hostpolicy.dll" = ".NET host policy (CRITICAL)"
    "coreclr.dll" = ".NET Core runtime (CRITICAL)"
    "System.Private.CoreLib.dll" = ".NET base library (CRITICAL)"
    "wpfgfx_cor3.dll" = "WPF graphics (for WPF apps)"
}

Write-Host "??? File Verification ???" -ForegroundColor Yellow
$missingFiles = @()
$foundFiles = @()

foreach ($file in $criticalFiles.Keys) {
    $filePath = Join-Path $fullPublishPath $file
 $description = $criticalFiles[$file]
    
    if (Test-Path $filePath) {
    $fileInfo = Get-Item $filePath
        Write-Host "? $file" -ForegroundColor Green -NoNewline
        Write-Host " ($([math]::Round($fileInfo.Length / 1KB, 1)) KB)" -ForegroundColor Gray
     $foundFiles += $file
  } else {
        Write-Host "? $file - MISSING ($description)" -ForegroundColor Red
      $missingFiles += $file
    }
}

# Check runtimeconfig.json content
Write-Host "`n??? Runtime Configuration Check ???" -ForegroundColor Yellow
$runtimeConfigPath = Join-Path $fullPublishPath "TimeTrackv2.runtimeconfig.json"

if (Test-Path $runtimeConfigPath) {
    try {
    $runtimeConfig = Get-Content $runtimeConfigPath -Raw | ConvertFrom-Json
        
        if ($runtimeConfig.runtimeOptions.includedFrameworks) {
            Write-Host "? includedFrameworks found (self-contained)" -ForegroundColor Green
            Write-Host "Frameworks:" -ForegroundColor Gray
  foreach ($fw in $runtimeConfig.runtimeOptions.includedFrameworks) {
           Write-Host "    - $($fw.name) $($fw.version)" -ForegroundColor Gray
            }
        } else {
     Write-Host "? includedFrameworks NOT found (framework-dependent)" -ForegroundColor Red
          Write-Host "  This is a FRAMEWORK-DEPENDENT build!" -ForegroundColor Red
     }
        
  if ($runtimeConfig.runtimeOptions.tfm) {
      Write-Host "  Target Framework: $($runtimeConfig.runtimeOptions.tfm)" -ForegroundColor Gray
        }
      
   if ($runtimeConfig.runtimeOptions.framework) {
            Write-Host "? framework property found (suggests framework-dependent)" -ForegroundColor Yellow
    Write-Host "  Framework: $($runtimeConfig.runtimeOptions.framework.name) $($runtimeConfig.runtimeOptions.framework.version)" -ForegroundColor Yellow
 }
    } catch {
        Write-Host "? Could not parse runtimeconfig.json: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "? runtimeconfig.json not found" -ForegroundColor Red
}

# Count total files
Write-Host "`n??? File Count Summary ???" -ForegroundColor Yellow
$allFiles = Get-ChildItem $fullPublishPath -File
$dllCount = ($allFiles | Where-Object { $_.Extension -eq ".dll" }).Count
$totalSize = ($allFiles | Measure-Object -Property Length -Sum).Sum

Write-Host "Total files: $($allFiles.Count)" -ForegroundColor Gray
Write-Host "DLL files: $dllCount" -ForegroundColor Gray
Write-Host "Total size: $([math]::Round($totalSize / 1MB, 1)) MB" -ForegroundColor Gray

# Self-contained builds are typically 60-150 MB
# Framework-dependent builds are typically 5-20 MB
if ($totalSize -lt 30MB) {
    Write-Host "? Size seems small for self-contained (typically 60+ MB)" -ForegroundColor Yellow
}

# Final verdict
Write-Host "`n??? VERDICT ???" -ForegroundColor Cyan

if ($missingFiles.Count -eq 0 -and (Test-Path $runtimeConfigPath)) {
    $rc = Get-Content $runtimeConfigPath -Raw | ConvertFrom-Json
    if ($rc.runtimeOptions.includedFrameworks) {
  Write-Host "? This appears to be a SELF-CONTAINED build" -ForegroundColor Green
     Write-Host "  Copy ALL files from this folder to your work PC" -ForegroundColor White
        Write-Host "  Folder: $fullPublishPath" -ForegroundColor White
    } else {
        Write-Host "? This is a FRAMEWORK-DEPENDENT build" -ForegroundColor Red
        Write-Host "`nTo fix, run:" -ForegroundColor Yellow
        Write-Host "  dotnet publish -c Release" -ForegroundColor White
        Write-Host "  (or)" -ForegroundColor Gray
      Write-Host "  dotnet publish -p:PublishProfile=Win64-SelfContained" -ForegroundColor White
    }
} else {
    Write-Host "? This is likely a FRAMEWORK-DEPENDENT or INCOMPLETE build" -ForegroundColor Red
    Write-Host "`nMissing critical files:" -ForegroundColor Yellow
    $missingFiles | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    Write-Host "`nTo fix, run:" -ForegroundColor Yellow
    Write-Host "  dotnet clean" -ForegroundColor White
    Write-Host "  dotnet publish -c Release" -ForegroundColor White
}

Write-Host "`n??? What to Copy ???" -ForegroundColor Cyan
Write-Host "1. Copy the ENTIRE contents of: $fullPublishPath" -ForegroundColor White
Write-Host "2. Paste to your work PC (e.g., C:\Apps\TimeTrack)" -ForegroundColor White
Write-Host "3. Run TimeTrackv2.exe from that folder" -ForegroundColor White
Write-Host "`nDO NOT copy just the EXE - you need ALL files!" -ForegroundColor Yellow

Write-Host ""
