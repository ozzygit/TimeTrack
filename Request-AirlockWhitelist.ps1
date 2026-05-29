# Request-AirlockWhitelist.ps1
# Generates information needed to request whitelisting in Airlock/security software

param(
    [string]$PublishPath = "bin\publish\win-x64"
)

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Airlock Whitelist Request Generator" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Auto-detect project root
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = $scriptDir

while ($projectRoot -and -not (Get-ChildItem -Path $projectRoot -Filter "*.csproj" -ErrorAction SilentlyContinue)) {
    $parent = Split-Path -Parent $projectRoot
    if ($parent -eq $projectRoot) { break }
    $projectRoot = $parent
}

$fullPublishPath = Join-Path $projectRoot $PublishPath

if (-not (Test-Path $fullPublishPath)) {
    Write-Host "? Publish folder not found: $fullPublishPath" -ForegroundColor Red
    exit 1
}

# Get all DLLs and EXEs
$files = Get-ChildItem $fullPublishPath -Include "*.dll","*.exe" -Recurse

# Group by publisher/vendor
$grouped = $files | ForEach-Object {
    $fileInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($_.FullName)
[PSCustomObject]@{
FileName = $_.Name
        FilePath = $_.FullName
    Company = $fileInfo.CompanyName
        Product = $fileInfo.ProductName
        Version = $fileInfo.FileVersion
  Size = $_.Length
        Hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
    }
} | Group-Object Company | Sort-Object Count -Descending

# Generate report
$reportPath = Join-Path $projectRoot "AIRLOCK_WHITELIST_REQUEST.md"

$report = @"
# TimeTrack v2 - Airlock Whitelist Request

**Application:** TimeTrack v2  
**Purpose:** MSP time tracking application  
**Deployment Type:** Self-contained .NET 8 application  
**Installation Path:** C:\Apps\TimeTrack (or similar)

---

## Executive Summary

TimeTrack v2 is a self-contained .NET 8 application for time tracking. It requires whitelisting of the main executable and its dependencies to run in an Airlock-protected environment.

**Total files requiring whitelist:** $($files.Count)  
- Executables: $(($files | Where-Object Extension -eq '.exe').Count)
- Libraries: $(($files | Where-Object Extension -eq '.dll').Count)

---

## Recommended Whitelisting Approaches

### Option 1: Code Signing (Preferred)
If the application is code-signed, whitelist by:
- **Certificate**: [Publisher Certificate Common Name]
- **Thumbprint**: [Certificate Thumbprint]

### Option 2: Path-Based Whitelist
Whitelist all files in the installation directory:
- **Path**: ``C:\Apps\TimeTrack\**\*``
- **Recursive**: Yes

### Option 3: Hash-Based Whitelist
Whitelist specific file hashes (see File Manifest below)

---

## Core Application Files

### Main Executable
``````
File: TimeTrackv2.exe
Purpose: Main application executable
Publisher: TimeTrack Project
Hash: $((Get-FileHash (Join-Path $fullPublishPath "TimeTrackv2.exe") -Algorithm SHA256).Hash)
``````

### Application DLL
``````
File: TimeTrackv2.dll
Purpose: Application logic
Publisher: TimeTrack Project  
Hash: $((Get-FileHash (Join-Path $fullPublishPath "TimeTrackv2.dll") -Algorithm SHA256).Hash)
``````

---

## Dependencies by Vendor

"@

foreach ($group in $grouped) {
    $vendor = if ($group.Name) { $group.Name } else { "Unknown/Third-party" }
    $report += "`n### $vendor`n"
    $report += "**File count:** $($group.Count)`n`n"
    
    $report += "| File | Version | Size (KB) |`n"
    $report += "|------|---------|-----------|`n"
    
    foreach ($file in ($group.Group | Sort-Object FileName)) {
        $sizeKB = [math]::Round($file.Size / 1KB, 1)
        $report += "| ``$($file.FileName)`` | $($file.Version) | $sizeKB |`n"
    }
    
    $report += "`n"
}

$report += @"

---

## Full File Manifest with Hashes

For hash-based whitelisting, here are all files with SHA256 hashes:

``````csv
FileName,FilePath,SHA256Hash,Size,Version,Company
"@

foreach ($file in ($files | Sort-Object Name)) {
    $fileInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($file.FullName)
    $hash = (Get-FileHash $file.FullName -Algorithm SHA256).Hash
    $relativePath = $file.FullName.Substring($fullPublishPath.Length + 1)
  $company = if ($fileInfo.CompanyName) { $fileInfo.CompanyName } else { "N/A" }
    $version = if ($fileInfo.FileVersion) { $fileInfo.FileVersion } else { "N/A" }
    
    $report += "`n$($file.Name),`"$relativePath`",$hash,$($file.Length),$version,`"$company`""
}

$report += @"
``````

---

## Security Justification

### Why These Files Are Safe

1. **Official .NET Runtime**: Many DLLs are part of the official Microsoft .NET 8 runtime
2. **Trusted Third-party Libraries**: 
   - Entity Framework Core (Microsoft)
   - SQLite (D. Richard Hipp, open source)
   - MVVM Toolkit (Microsoft Community)
3. **Internal Application Code**: TimeTrackv2.dll/exe (can be code-signed)

### Risk Assessment: LOW
- Application runs with standard user privileges
- No network communication (local-only)
- No kernel-mode drivers
- No system modification
- Database stored in user profile only

---

## Testing Recommendation

1. Deploy to test environment with Airlock enabled
2. Apply whitelist rules
3. Test application startup and core functionality
4. Monitor Airlock logs for any additional blocks
5. Adjust whitelist as needed

---

## Contact Information

**Application Owner:** [Your Name/Team]
**Email:** [Support Email]  
**GitHub:** https://github.com/Bukinnear/TimeTrack

---

## Appendix: Alternative Deployment (Framework-Dependent)

If whitelisting all dependencies is not feasible, consider a framework-dependent deployment:
- **Requires:** .NET 8 Desktop Runtime installed on PCs
- **Reduces files:** ~150 files ? ~50 files
- **Trade-off:** Users must have .NET 8 installed system-wide

However, many of the blocked DLLs (SQLite, EF Core) would still be included.

---

*Generated on: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")*  
*Publish path: $fullPublishPath*
"@

# Save report
$report | Out-File $reportPath -Encoding UTF8

Write-Host "? Whitelist request document generated" -ForegroundColor Green
Write-Host "Location: $reportPath`n" -ForegroundColor White

# Display summary
Write-Host "Summary:" -ForegroundColor Yellow
Write-Host "  Total files: $($files.Count)" -ForegroundColor Gray
Write-Host "  Executables: $(($files | Where-Object Extension -eq '.exe').Count)" -ForegroundColor Gray
Write-Host "  Libraries: $(($files | Where-Object Extension -eq '.dll').Count)" -ForegroundColor Gray

Write-Host "`nTop vendors:" -ForegroundColor Yellow
$grouped | Select-Object -First 5 | ForEach-Object {
$vendor = if ($_.Name) { $_.Name } else { "Unknown" }
    Write-Host "  $vendor`: $($_.Count) files" -ForegroundColor Gray
}

Write-Host "`nNext steps:" -ForegroundColor Yellow
Write-Host "  1. Review: $reportPath" -ForegroundColor White
Write-Host "  2. Submit to IT/Security team for whitelisting" -ForegroundColor White
Write-Host "  3. Consider code signing to simplify whitelisting" -ForegroundColor White
Write-Host ""

# Offer to open the document
$response = Read-Host "Open the document now? (y/n)"
if ($response -eq 'y') {
    Start-Process $reportPath
}
