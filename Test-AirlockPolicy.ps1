# Test-AirlockPolicy.ps1
# Quick test to understand what Airlock is blocking

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Airlock Policy Detection Test" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$publishPath = "bin\publish\win-x64"

if (-not (Test-Path $publishPath)) {
    Write-Host "? Publish folder not found: $publishPath" -ForegroundColor Red
    Write-Host "Run 'dotnet publish -c Release' first" -ForegroundColor Yellow
    exit 1
}

# Files that Airlock is blocking
$blockedFiles = @(
    "Microsoft.Extensions.Configuration.Abstractions.dll",
    "Microsoft.Extensions.Primitives.dll",
    "SQLitePCLRaw.core.dll",
    "SQLitePCLRaw.batteries_v2.dll",
    "SQLitePCLRaw.provider.e_sqlite3.dll"
)

Write-Host "Analyzing blocked files...`n" -ForegroundColor Yellow

foreach ($file in $blockedFiles) {
    $filePath = Join-Path $publishPath $file
    
    if (-not (Test-Path $filePath)) {
        Write-Host "? $file - NOT FOUND" -ForegroundColor DarkGray
        continue
    }
    
    Write-Host "??? $file ???" -ForegroundColor Cyan
    
    $fileInfo = Get-Item $filePath
    $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($filePath)
    
    # Check if signed
    $signature = Get-AuthenticodeSignature $filePath
    
    Write-Host "  Size: $([math]::Round($fileInfo.Length / 1KB, 1)) KB" -ForegroundColor Gray
    Write-Host "  Company: $($versionInfo.CompanyName)" -ForegroundColor Gray
    Write-Host "  Product: $($versionInfo.ProductName)" -ForegroundColor Gray
    Write-Host "  Version: $($versionInfo.FileVersion)" -ForegroundColor Gray
    
    if ($signature.Status -eq 'Valid') {
      Write-Host "  ? SIGNED by: $($signature.SignerCertificate.Subject)" -ForegroundColor Green
        Write-Host "  Thumbprint: $($signature.SignerCertificate.Thumbprint)" -ForegroundColor Green
    }
    else {
      Write-Host "  ? NOT SIGNED" -ForegroundColor Yellow
    }
    
    Write-Host ""
}

Write-Host "`n??? Analysis ???" -ForegroundColor Cyan

$signedCount = 0
$unsignedCount = 0

foreach ($file in $blockedFiles) {
    $filePath = Join-Path $publishPath $file
    if (Test-Path $filePath) {
        $sig = Get-AuthenticodeSignature $filePath
        if ($sig.Status -eq 'Valid') {
            $signedCount++
        } else {
 $unsignedCount++
   }
    }
}

Write-Host "Blocked files:" -ForegroundColor White
Write-Host "  Already signed: $signedCount" -ForegroundColor $(if ($signedCount -gt 0) { "Green" } else { "Gray" })
Write-Host "  Not signed: $unsignedCount" -ForegroundColor Yellow

Write-Host "`n??? Recommendations ???" -ForegroundColor Cyan

if ($signedCount -gt 0) {
    Write-Host "??  Some blocked files are ALREADY SIGNED!" -ForegroundColor Yellow
    Write-Host "   This means Airlock is NOT trusting signed code automatically." -ForegroundColor Yellow
    Write-Host "`n? Code signing YOUR app won't help with THESE files" -ForegroundColor Red
  Write-Host "   ? You need IT to whitelist by path or hash" -ForegroundColor White
}
else {
    Write-Host "??  None of the blocked files are currently signed." -ForegroundColor Gray
    Write-Host "`n   Possible strategies:" -ForegroundColor White
    Write-Host "   1. Ask IT to whitelist C:\Apps\TimeTrack\** (RECOMMENDED)" -ForegroundColor Green
    Write-Host "   2. Sign your EXE/DLL and see if that helps reputation" -ForegroundColor Yellow
    Write-Host "   3. Request hash-based whitelist for these specific files" -ForegroundColor Gray
}

Write-Host "`n??? Questions for Your IT Team ???" -ForegroundColor Cyan
Write-Host "1. Does Airlock trust code-signed applications automatically?" -ForegroundColor White
Write-Host "2. Can you whitelist by installation path (e.g., C:\Apps\TimeTrack\**)?" -ForegroundColor White
Write-Host "3. What information do you need for whitelist approval?" -ForegroundColor White
Write-Host "4. Are there any signed third-party DLLs that Airlock allows?" -ForegroundColor White

Write-Host ""
