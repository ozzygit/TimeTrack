# Running TimeTrack from Different Locations - Troubleshooting Guide

**Issue:** Published application runs from the release folder but fails when moved to `c:\temp`

---

## Understanding the Application Architecture

### Data Storage Locations

TimeTrack v2 uses **user-profile-dependent paths** that are **independent of the executable location**:

1. **Database Location:**
   - Path: `%APPDATA%\TimeTrack v2\timetrack_v2.db`
   - Actual: `C:\Users\<username>\AppData\Roaming\TimeTrack v2\`
   - **This does NOT change** when you move the executable

2. **Log File Location:**
   - Path: `%APPDATA%\TimeTrack v2\time_track_log.txt`
   - Actual: `C:\Users\<username>\AppData\Roaming\TimeTrack v2\`
   - **This does NOT change** when you move the executable

3. **Environment Variable Override:**
   - You can set `TIMETRACK_APPDATA` to use a custom location
   - Example: `$env:TIMETRACK_APPDATA = "C:\temp\TimeTrackData"`

**Note:** Both database and logs are stored in the same directory (`%APPDATA%\TimeTrack v2\`) for consistency.

### What Should Work

? The application should run from **any location** because:
- Data is stored in user profile directories
- No relative paths to the executable location
- No dependency on working directory
- Self-contained publish includes all dependencies

---

## Common Issues When Moving the Application

### 1. ?? Single-File Extraction Issues

**Problem:** When published as `PublishSingleFile=true`, .NET extracts native libraries to a temp folder.

**Symptoms:**
- Application doesn't start
- No error message visible
- Process appears in Task Manager briefly then exits

**Solution:**
```xml
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <PublishSingleFile>true</PublishSingleFile>
  <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  <IncludeAllContentForSelfExtract>true</IncludeAllContentForSelfExtract>
</PropertyGroup>
```

**Current Status:** ? `IncludeNativeLibrariesForSelfExtract` is already enabled

### 2. ?? SQLite Native Libraries

**Problem:** SQLite requires native DLLs (`e_sqlite3.dll`) that must be properly extracted.

**Symptoms:**
- DllNotFoundException for `e_sqlite3`
- Application crashes on database initialization
- Silent failure with no visible error

**Check:**
```powershell
# After running the app once, check temp extraction folder
Get-ChildItem $env:TEMP -Recurse -Filter "e_sqlite3.dll" | Select-Object FullName
```

**Solution:**
- Ensure `IncludeNativeLibrariesForSelfExtract` is set to `true` ?
- Check that SQLite provider is correctly referenced in `.csproj` ?

### 3. ?? File System Permissions

**Problem:** `c:\temp` might have restricted permissions depending on your system configuration.

**Symptoms:**
- Access denied errors
- Unable to extract native libraries
- Unable to write to temp extraction folder

**Check Permissions:**
```powershell
# Check permissions on c:\temp
Get-Acl "C:\temp" | Format-List

# Check if you can write to the directory
Test-Path "C:\temp" -IsValid
New-Item "C:\temp\test.txt" -ItemType File -Force
Remove-Item "C:\temp\test.txt" -Force
```

**Solution:**
- Run as administrator (temporary test)
- Move to a different location like `C:\Users\<username>\Desktop\TimeTrack`
- Check antivirus/security software blocking execution

### 4. ?? Antivirus / Security Software

**Problem:** Security software might block execution from certain directories.

**Symptoms:**
- Executable runs from release folder but not from `c:\temp`
- No error message
- Process killed immediately

**Check:**
- Windows Defender SmartScreen
- Corporate antivirus (e.g., Airlock, mentioned in your code comments)
- Windows Security settings

**Solution:**
- Check Windows Event Viewer for security blocks
- Add exception for the executable
- Test in a different directory

### 5. ?? Working Directory Issues

**Problem:** Some code might depend on the current working directory.

**Symptoms:**
- Application fails to find resources
- FileNotFoundException for embedded resources
- XAML loading errors

**Current Status:** ? Your code uses absolute paths (AppData) so this should not be an issue

---

## Diagnostic Steps

### Step 1: Run with Enhanced Logging

The application now includes enhanced startup diagnostics. When you run it:

1. **Check Debug Output:**
   - Run from Visual Studio with debugger attached
   - Check Output window for diagnostic messages
   - Look for the "Startup Diagnostics" section

2. **Check Log File:**
   ```powershell
   Get-Content "$env:APPDATA\TimeTrack v2\time_track_log.txt" -Tail 50
   ```

3. **Run from Command Line:**
   ```powershell
   cd c:\temp
   .\TimeTrack.exe
   ```
   - If an error occurs, it should now display a MessageBox with details

### Step 2: Check File Extraction

When running a single-file application, .NET extracts files to a temp directory:

```powershell
# Find the extraction directory
$extractDirs = Get-ChildItem $env:TEMP -Directory -Filter ".net" -Recurse -ErrorAction SilentlyContinue
$extractDirs | ForEach-Object {
    Get-ChildItem $_.FullName -Recurse -Filter "*.dll" | Select-Object Name, FullName
}
```

**Expected files:**
- `e_sqlite3.dll` (SQLite native library)
- `TimeTrack.dll`
- Other .NET runtime DLLs

### Step 3: Test Database Creation

Create a test script to verify database initialization:

```powershell
# Test-DatabaseInit.ps1
$env:TIMETRACK_APPDATA = "C:\temp\TestTimeTrackData"

# Run the app
& "C:\temp\TimeTrack.exe"

# Check if database was created
if (Test-Path "$env:TIMETRACK_APPDATA\TimeTrack v2\timetrack_v2.db") {
    Write-Host "? Database created successfully" -ForegroundColor Green
} else {
    Write-Host "? Database NOT created" -ForegroundColor Red
}

# Clean up
Remove-Item -Recurse -Force "$env:TIMETRACK_APPDATA" -ErrorAction SilentlyContinue
```

### Step 4: Check Windows Event Viewer

Security issues might be logged in Event Viewer:

```powershell
# Check Application errors
Get-EventLog -LogName Application -Source ".NET Runtime" -Newest 10 -After (Get-Date).AddHours(-1)

# Check Security blocks
Get-EventLog -LogName Security -Newest 10 -After (Get-Date).AddHours(-1) | 
    Where-Object { $_.Message -like "*TimeTrack*" }
```

### Step 5: Compare Working vs Non-Working Locations

```powershell
# Test script to compare execution from different locations

function Test-TimeTrackLocation {
    param([string]$Location)
    
    Write-Host "`n=== Testing from: $Location ===" -ForegroundColor Cyan
    
    # Copy executable
    Copy-Item "bin\Release\net8.0-windows\win-x64\publish\TimeTrack.exe" $Location -Force
    
    # Try to run
    $process = Start-Process -FilePath "$Location\TimeTrack.exe" -PassThru -WindowStyle Hidden
    Start-Sleep -Seconds 2
    
    if ($process.HasExited) {
        Write-Host "? Process exited immediately (Exit Code: $($process.ExitCode))" -ForegroundColor Red
    } else {
        Write-Host "? Process is running" -ForegroundColor Green
        Stop-Process -Id $process.Id -Force
    }
}

# Test different locations
Test-TimeTrackLocation "C:\Users\$env:USERNAME\Desktop"
Test-TimeTrackLocation "C:\temp"
Test-TimeTrackLocation "$env:USERPROFILE\Documents"
```

---

## What You Should Try

### 1. Check if the Application Actually Starts

```powershell
# Run from c:\temp and monitor
cd c:\temp
.\TimeTrack.exe

# In another PowerShell window, check if process is running
Get-Process TimeTrack -ErrorAction SilentlyContinue
```

### 2. Check for Error Dialog

With the improved error handling, if the app fails to start, you should now see:
- A MessageBox with detailed error information
- Diagnostic paths showing where it's trying to access
- The actual exception message

**If you don't see an error dialog:**
- The process might be killed by security software
- There might be a crash before exception handlers are registered
- Check Task Manager for the process appearing and disappearing

### 3. Run with Debugger Attached

```powershell
# Start Visual Studio with debugger
# Set TimeTrack.exe in c:\temp as the startup executable
# Run and see where it crashes
```

### 4. Test with Environment Variable Override

```powershell
# Force a specific data location
$env:TIMETRACK_APPDATA = "C:\temp\TimeTrackData"
cd c:\temp
.\TimeTrack.exe

# Check if it created the directory
Test-Path "C:\temp\TimeTrackData\TimeTrack v2"
```

---

## Recommended Publishing Configuration

### For Maximum Compatibility

Update `TimeTrack.csproj` to ensure proper extraction:

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <SelfContained>true</SelfContained>
  <PublishSingleFile>true</PublishSingleFile>
  
  <!-- Ensure all native libraries are embedded and extracted -->
  <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  <IncludeAllContentForSelfExtract>true</IncludeAllContentForSelfExtract>
  
  <!-- Enable ready-to-run compilation for faster startup -->
  <PublishReadyToRun>false</PublishReadyToRun>
  
  <!-- Trim unused code (optional, but reduces size) -->
  <PublishTrimmed>false</PublishTrimmed>
</PropertyGroup>
```

**Note:** Your current configuration already has the essential settings ?

### Publishing Commands

```powershell
# Clean build
dotnet clean

# Publish for release
dotnet publish -c Release -r win-x64 --self-contained

# Output will be in:
# bin\Release\net8.0-windows\win-x64\publish\TimeTrack.exe
```

---

## What to Check in Your Environment

### 1. Verify SQLite Dependency

```powershell
# Check that SQLite package is correctly referenced
dotnet list package --include-transitive | Select-String -Pattern "sqlite"
```

**Expected output:**
```
> Microsoft.EntityFrameworkCore.Sqlite      8.0.10
  > Microsoft.Data.Sqlite.Core              8.0.10
    > SQLitePCLRaw.core                      2.1.6
    > SQLitePCLRaw.bundle_e_sqlite3          2.1.6
```

### 2. Verify Published Files

```powershell
# Check what's in the publish directory
Get-ChildItem "bin\Release\net8.0-windows\win-x64\publish" | Select-Object Name, Length
```

**For single-file publish, you should see:**
- `TimeTrack.exe` (large, ~70-100MB for self-contained)
- `TimeTrack.pdb` (debug symbols, optional)

### 3. Test Double-Click vs Command Line

Sometimes there's a difference:

- **Double-click:** Working directory = executable directory
- **Command line:** Working directory = current directory

**Test both:**
```powershell
# Test 1: Navigate and run
cd c:\temp
.\TimeTrack.exe

# Test 2: Run from different directory
cd c:\
& "c:\temp\TimeTrack.exe"

# Test 3: Start-Process
Start-Process "c:\temp\TimeTrack.exe" -WorkingDirectory "c:\temp"
```

---

## Expected Behavior After Improvements

With the enhanced error handling in `App.xaml.cs`, you should now see:

### ? If Database Initialization Fails:
```
TimeTrack v2 - Startup Error

TimeTrack v2 failed to start.

Error: [Specific error message]

Diagnostic Information:
- Executable: C:\temp\
- Working Directory: C:\temp
- User Profile: C:\Users\<username>
- AppData: C:\Users\<username>\AppData\Roaming

The application may not have sufficient permissions or the database location may be inaccessible.

Full Exception:
[Stack trace]
```

### ? If Window Creation Fails:
```
TimeTrack v2 - Window Creation Error

Failed to create the main window.

Error: [Specific error message]

The application will now exit.
```

### ? If Everything Works:
- Application starts normally
- Main window appears
- Database is created in `%APPDATA%\TimeTrack v2\`

---

## Quick Test Script

Save this as `Test-TimeTrackFromTemp.ps1`:

```powershell
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

# Try to run
Write-Host "`nAttempting to run TimeTrack.exe from c:\temp..." -ForegroundColor Yellow
Write-Host "Press Ctrl+C to stop monitoring`n" -ForegroundColor Gray

$process = Start-Process -FilePath "C:\temp\TimeTrack.exe" -PassThru

# Monitor for 5 seconds
for ($i = 0; $i -lt 10; $i++) {
    Start-Sleep -Milliseconds 500
    
    if ($process.HasExited) {
        Write-Host "? Process exited with code: $($process.ExitCode)" -ForegroundColor Red
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

# Check log file
$logPath = "$env:APPDATA\TimeTrack v2\time_track_log.txt"
if (Test-Path $logPath) {
    Write-Host "`nRecent log entries:" -ForegroundColor Cyan
    Get-Content $logPath -Tail 5
}

Write-Host "`nTest complete." -ForegroundColor Cyan
```

**Run it:**
```powershell
.\Test-TimeTrackFromTemp.ps1
```

---

## Next Steps

1. **Rebuild and publish:**
   ```powershell
   dotnet clean
   dotnet publish -c Release -r win-x64 --self-contained
   ```

2. **Copy to c:\temp and try to run:**
   ```powershell
   Copy-Item "bin\Release\net8.0-windows\win-x64\publish\TimeTrack.exe" "C:\temp\" -Force
   cd c:\temp
   .\TimeTrack.exe
   ```

3. **You should now see an error dialog if it fails** with specific diagnostic information

4. **Report back what you see:**
   - Does an error dialog appear?
   - What does it say?
   - Check the log file at `%APPDATA%\TimeTrack v2\time_track_log.txt`
   - Check Windows Event Viewer

---

## Summary

The issue is **NOT related to data storage locations** because:
- ? Database is stored in `%APPDATA%` (independent of executable location)
- ? Logs are stored in `%APPDATA%` (same location, independent of executable location)
- ? No relative paths used

The issue is **LIKELY related to**:
- ? File system permissions on `c:\temp`
- ? Security software blocking execution
- ? Single-file extraction issues
- ? SQLite native library extraction failure

**The enhanced error handling should now tell you exactly what's wrong.**
