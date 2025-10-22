# Sign-TimeTrack.ps1
# Code signing script for TimeTrack v2

param(
    [Parameter(Mandatory=$false)]
    [string]$CertificateThumbprint,
    
    [Parameter(Mandatory=$false)]
    [string]$TimestampServer = "http://timestamp.digicert.com"
)

$exePath = "bin\Release\net8.0-windows\win-x64\publish\TimeTrack.exe"

if (-not (Test-Path $exePath)) {
    Write-Host "? Executable not found at: $exePath" -ForegroundColor Red
    Write-Host "Run 'dotnet publish -c Release' first" -ForegroundColor Yellow
    exit 1
}

# If no certificate specified, try to find one in the user's certificate store
if ([string]::IsNullOrEmpty($CertificateThumbprint)) {
    Write-Host "?? Looking for code signing certificates..." -ForegroundColor Yellow
    
    $certs = Get-ChildItem -Path Cert:\CurrentUser\My -CodeSigningCert
    
    if ($certs.Count -eq 0) {
        Write-Host "? No code signing certificates found" -ForegroundColor Red
        Write-Host "`nTo sign the application, you need:" -ForegroundColor Yellow
        Write-Host "1. A code signing certificate from your organization" -ForegroundColor White
        Write-Host "2. Or a self-signed certificate for testing:" -ForegroundColor White
        Write-Host "   New-SelfSignedCertificate -Type CodeSigningCert -Subject 'CN=TimeTrack Dev' -CertStoreLocation Cert:\CurrentUser\My" -ForegroundColor Gray
        exit 1
    }
    
    Write-Host "?? Available certificates:" -ForegroundColor Green
    $certs | ForEach-Object { Write-Host "  - $($_.Subject) [$($_.Thumbprint)]" -ForegroundColor White }
    
    if ($certs.Count -eq 1) {
        $cert = $certs[0]
        Write-Host "`n? Using: $($cert.Subject)" -ForegroundColor Green
    } else {
        Write-Host "`n?? Multiple certificates found. Specify thumbprint with -CertificateThumbprint" -ForegroundColor Yellow
        exit 1
    }
} else {
    $cert = Get-Item "Cert:\CurrentUser\My\$CertificateThumbprint" -ErrorAction SilentlyContinue
    if (-not $cert) {
        Write-Host "? Certificate not found: $CertificateThumbprint" -ForegroundColor Red
        exit 1
    }
}

# Sign the executable
Write-Host "`n?? Signing TimeTrack.exe..." -ForegroundColor Cyan
try {
    Set-AuthenticodeSignature -FilePath $exePath -Certificate $cert -TimestampServer $TimestampServer -HashAlgorithm SHA256
    
    Write-Host "? Application signed successfully!" -ForegroundColor Green
    
    # Verify signature
    $signature = Get-AuthenticodeSignature -FilePath $exePath
    Write-Host "`n?? Signature details:" -ForegroundColor Cyan
    Write-Host "  Status: $($signature.Status)" -ForegroundColor White
    Write-Host "  Signer: $($signature.SignerCertificate.Subject)" -ForegroundColor White
    Write-Host "  Timestamp: $($signature.TimeStamperCertificate.Subject)" -ForegroundColor White
    
} catch {
    Write-Host "? Signing failed: $_" -ForegroundColor Red
    exit 1
}
