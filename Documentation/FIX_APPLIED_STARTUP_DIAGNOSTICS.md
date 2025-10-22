# Fix Applied: Enhanced Startup Error Handling

**Date:** 2025-01-19
**Issue:** Application runs from release folder but not from `c:\temp`
**Status:** ? Diagnostic improvements applied

---

## What Was Changed

### 1. Enhanced Error Handling in `App.xaml.cs`

**Before:**
- Minimal startup error handling
- Silent failures during database initialization
- No diagnostic information on startup failure

**After:**
- ? Comprehensive exception handling in `OnStartup()`
- ? Detailed diagnostic information on failure
- ? Debug output showing all relevant paths
- ? MessageBox with full error details if startup fails
- ? Graceful shutdown with exit code 1 on critical failures

**Changes:**
```csharp
// Added detailed diagnostic output
System.Diagnostics.Debug.WriteLine("=== TimeTrack v2 Startup Diagnostics ===");
System.Diagnostics.Debug.WriteLine($"Executable Location: {AppDomain.CurrentDomain.BaseDirectory}");
System.Diagnostics.Debug.WriteLine($"Current Directory: {Environment.CurrentDirectory}");
// ... more diagnostics

// Added comprehensive error handling with diagnostic MessageBox
catch (Exception ex)
{
    string diagnosticInfo = 
        $"TimeTrack v2 failed to start.\n\n" +
        $"Error: {ex.Message}\n\n" +
        $"Diagnostic Information:\n" +
        $"- Executable: {AppDomain.CurrentDomain.BaseDirectory}\n" +
        // ... more details
    
    MessageBox.Show(diagnosticInfo, "TimeTrack v2 - Startup Error", ...);
    Shutdown(1);
}

// Separate error handling for window creation
try {
    var mainWindow = new MainWindow();
    mainWindow.Show();
} catch (Exception ex) {
    ErrorHandler.Handle("Failed to create main window.", ex);
    MessageBox.Show(...);
    Shutdown(1);
}
```

---

## Why This Helps

### Previous Behavior
When the application failed to start from `c:\temp`:
- ? Process would start and immediately exit
- ? No visible error message
- ? No way to know what failed
- ? Had to use debugger or Event Viewer

### New Behavior
When the application fails to start:
- ? **MessageBox with detailed error** appears immediately
- ? Shows exact exception message
- ? Shows diagnostic paths (executable location, working directory, AppData, etc.)
- ? Shows full exception stack trace
- ? Logs to Debug output window
- ? Logs to file at `%LOCALAPPDATA%\TimeTrack v2\time_track_log.txt`

---

## How to Use the New Diagnostic Features

### Step 1: Rebuild and Publish

```powershell
# Clean and publish
dotnet clean
dotnet publish -c Release -r win-x64 --self-contained
```

### Step 2: Run the Test Script

Use the provided PowerShell script to automatically test:

```powershell
.\Publish-And-Test.ps1
```

**Or manually:**

```powershell
# Copy to c:\temp
Copy-Item "bin\Release\net8.0-windows\win-x64\publish\TimeTrack.exe" "C:\temp\" -Force

# Run from c:\temp
cd c:\temp
.\TimeTrack.exe
```

### Step 3: Observe the Results

#### ? If It Works:
- Application opens normally
- Main window appears
- Database created in `%APPDATA%\TimeTrack v2\`

#### ? If It Fails:
You will now see a **detailed error dialog** like:

```
????????????????????????????????????????????????
? TimeTrack v2 - Startup Error              [X]?
????????????????????????????????????????????????
? TimeTrack v2 failed to start.                ?
?                                              ?
? Error: Could not load file or assembly...   ?
?                                              ?
? Diagnostic Information:                     ?
? - Executable: C:\temp\                      ?
? - Working Directory: C:\temp                 ?
? - User Profile: C:\Users\username            ?
? - AppData: C:\Users\username\AppData\...    ?
?                                              ?
? The application may not have sufficient      ?
? permissions or the database location may be  ?
? inaccessible.                                ?
?                                              ?
? Full Exception:                              ?
? [Complete stack trace]                       ?
?                                              ?
?                     [OK]                     ?
????????????????????????????????????????????????
```

---

## What to Check Based on Error Message

### Error: "Could not load file or assembly"
**Cause:** Missing or inaccessible DLL (likely SQLite)
**Solution:**
- Check that `IncludeNativeLibrariesForSelfExtract` is enabled ?
- Check temp extraction folder for `e_sqlite3.dll`
- Check antivirus blocking DLL extraction

### Error: "Access denied" or "UnauthorizedAccessException"
**Cause:** Insufficient permissions
**Solution:**
- Check `c:\temp` permissions
- Try a different directory (e.g., `C:\Users\username\Desktop`)
- Run as administrator (test only)

### Error: "DirectoryNotFoundException" or "PathTooLongException"
**Cause:** Path issues
**Solution:**
- Check that AppData folders are accessible
- Verify no OneDrive/network drive issues
- Check for path length limits

---

## Additional Diagnostic Tools

### 1. Test Script: `Test-TimeTrackFromTemp.ps1`

Comprehensive diagnostic script that checks:
- ? Executable copying
- ? File permissions
- ? Process execution
- ? Database creation
- ? Log file creation
- ? Windows Event Log
- ? SQLite library extraction

**Usage:**
```powershell
.\Test-TimeTrackFromTemp.ps1
```

### 2. Publish & Test Script: `Publish-And-Test.ps1`

Complete workflow automation:
- ? Clean build
- ? Publish
- ? Copy to c:\temp
- ? Test from release folder (control)
- ? Test from c:\temp
- ? Results summary with diagnostics

**Usage:**
```powershell
# Full workflow
.\Publish-And-Test.ps1

# Skip publish if already done
.\Publish-And-Test.ps1 -SkipPublish

# Open log file in Notepad
.\Publish-And-Test.ps1 -OpenLogs
```

### 3. Documentation: `RUNNING_FROM_DIFFERENT_LOCATIONS.md`

Complete troubleshooting guide covering:
- Architecture explanation
- Common issues and solutions
- Diagnostic procedures
- Configuration recommendations
- Expected behaviors

---

## What You Should See in Debug Output

When running with debugger attached, you'll now see:

```
=== TimeTrack v2 Startup Diagnostics ===
Executable Location: C:\temp\
Current Directory: C:\temp
User Profile: C:\Users\username
AppData (Roaming): C:\Users\username\AppData\Roaming
LocalAppData: C:\Users\username\AppData\Local
Database Location: C:\Users\username\AppData\Roaming\TimeTrack v2\timetrack_v2.db
Database initialized successfully
```

Or if it fails:

```
=== TimeTrack v2 Startup Diagnostics ===
Executable Location: C:\temp\
Current Directory: C:\temp
User Profile: C:\Users\username
AppData (Roaming): C:\Users\username\AppData\Roaming
LocalAppData: C:\Users\username\AppData\Local
Startup failed: System.IO.FileNotFoundException: Could not load file or assembly...
   at TimeTrack.Data.Database.CreateDatabase()
   at TimeTrack.App.OnStartup(StartupEventArgs e)
```

---

## Next Steps

1. **Rebuild with new error handling:**
   ```powershell
   dotnet clean
   dotnet build -c Release
   ```

2. **Run the test script:**
   ```powershell
   .\Publish-And-Test.ps1
   ```

3. **Report the results:**
   - Does an error dialog appear?
   - What does the error message say?
   - What's in the log file?
   - Any entries in Event Viewer?

4. **Follow the troubleshooting guide:**
   - See `RUNNING_FROM_DIFFERENT_LOCATIONS.md`
   - Address specific error messages
   - Check permissions, antivirus, etc.

---

## Summary

### What We Know:
- ? Application uses profile-independent data storage
- ? Database is stored in `%APPDATA%\TimeTrack v2\`
- ? No hardcoded paths to executable location
- ? Self-contained publish includes all dependencies
- ? Single-file publish configured correctly

### What We Added:
- ? Comprehensive startup error handling
- ? Detailed diagnostic information on failure
- ? Helpful error dialogs
- ? Debug output with path information
- ? Automated test scripts
- ? Complete troubleshooting documentation

### What You Should Do:
1. Rebuild the application
2. Run `Publish-And-Test.ps1`
3. If it fails, read the error dialog carefully
4. Follow the troubleshooting steps for that specific error
5. Report back what you find

**The error handling improvements will now tell you exactly what's wrong!**
