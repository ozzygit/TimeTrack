# Portable Mode Configuration - Summary

**Date:** 2025-01-XX  
**Change:** Switched from AppData storage to portable mode (database and logs beside executable)  
**Status:** ? Complete  
**Reason:** Avoid Airlock blocking on work machines

---

## ?? What Changed

### Database Location
**Before:**
```
C:\Users\{username}\AppData\Roaming\TimeTrack v2\timetrack_v2.db
```

**After:**
```
<exe-folder>\timetrack_v2.db
```

Example: If exe is at `C:\Apps\TimeTrack\TimeTrack.exe`, database will be at `C:\Apps\TimeTrack\timetrack_v2.db`

### Log File Location
**Before:**
```
C:\Users\{username}\AppData\Roaming\TimeTrack v2\time_track_log.txt
```

**After:**
```
<exe-folder>\time_track_log.txt
```

### Backup Location
**Before:**
```
C:\Users\{username}\AppData\Roaming\TimeTrack v2\Backups\
```

**After:**
```
<exe-folder>\Backups\
```

---

## ?? File Structure After Running

```
C:\YourFolder\
??? TimeTrack.exe                           (Application)
??? timetrack_v2.db                         (Database) ?
??? timetrack_v2.db-shm                     (SQLite shared memory)
??? timetrack_v2.db-wal                     (SQLite write-ahead log)
??? time_track_log.txt                      (Error logs) ?
??? Backups\                                (Daily backups) ?
    ??? timetrack_v2_backup_2025-01-19.db
    ??? timetrack_v2_backup_2025-01-20.db
    ??? ...
```

---

## ? Benefits

### 1. **Avoids Airlock Blocking**
- ? No access to `%APPDATA%` or `%LOCALAPPDATA%`
- ? No security software triggers
- ? Matches pre-.NET 8 version behavior

### 2. **Portable**
- ? Copy entire folder to another machine - just works
- ? No user profile dependencies
- ? Easy to backup (single folder)
- ? Works from USB drive or network share

### 3. **Transparent**
- ? Data is visible next to exe
- ? Easy to find and manage
- ? No hidden AppData folders

### 4. **Enterprise Friendly**
- ? Deploy to shared folder (e.g., `\\server\apps\TimeTrack\`)
- ? Each user runs from same location
- ? No per-user AppData confusion

---

## ?? Files Modified

### 1. `Data/Database.cs`
**Changed:**
- `GetAppFolder()` now returns `AppDomain.CurrentDomain.BaseDirectory`
- Removed all AppData references
- Removed migration code (no longer needed)
- Environment variable override still works: `TIMETRACK_APPDATA`

**Key Lines:**
```csharp
private static string GetAppFolder()
{
    var overridePath = Environment.GetEnvironmentVariable("TIMETRACK_APPDATA");
    if (!string.IsNullOrWhiteSpace(overridePath))
        return overridePath;

    // PORTABLE MODE: Store database in same folder as executable
    return AppDomain.CurrentDomain.BaseDirectory;
}
```

### 2. `Utilities/ErrorHandler.cs`
**Changed:**
- Log directory now uses `AppDomain.CurrentDomain.BaseDirectory`
- Logs stored beside executable instead of AppData

**Key Lines:**
```csharp
string logDir = AppDomain.CurrentDomain.BaseDirectory;
string logPath = Path.Combine(logDir, "time_track_log.txt");
```

---

## ?? Testing

### Build and Test Locally
```powershell
# Clean and rebuild
dotnet clean
dotnet publish -c Release -r win-x64 --self-contained

# Navigate to publish folder
cd bin\Release\net8.0-windows\win-x64\publish

# Run the application
.\TimeTrack.exe

# Verify files created in same folder
dir *.db
dir *.txt
dir Backups\
```

**Expected output:**
```
timetrack_v2.db
timetrack_v2.db-shm
timetrack_v2.db-wal
time_track_log.txt (if any errors occurred)
Backups\ (folder, created on first backup)
```

### Test on Work Machine (Airlock Environment)
```powershell
# Copy entire publish folder to work machine
# Example: C:\Apps\TimeTrack\

# Run from that location
cd C:\Apps\TimeTrack
.\TimeTrack.exe

# Should work without Airlock blocking!
```

---

## ?? Environment Variable Override

You can still override the location if needed:

### Set Custom Location
```powershell
# System-wide (requires admin)
[System.Environment]::SetEnvironmentVariable('TIMETRACK_APPDATA', 'D:\TimeTrack', 'Machine')

# Per-user
[System.Environment]::SetEnvironmentVariable('TIMETRACK_APPDATA', 'D:\TimeTrack', 'User')

# Temporary (current session only)
$env:TIMETRACK_APPDATA = 'D:\TimeTrack'
```

### Example: Network Share
```powershell
$env:TIMETRACK_APPDATA = '\\server\share\TimeTrack'
.\TimeTrack.exe
# Database will be at: \\server\share\TimeTrack\timetrack_v2.db
```

---

## ?? Deployment Instructions

### For IT Departments

1. **Copy published folder to deployment location:**
   ```
   \\server\apps\TimeTrack\
   ??? TimeTrack.exe
   ??? [all other DLLs and files]
   ```

2. **Create shortcut for users:**
   ```
   Target: \\server\apps\TimeTrack\TimeTrack.exe
   Start in: \\server\apps\TimeTrack
   ```

3. **Data will be stored at:**
   ```
   \\server\apps\TimeTrack\timetrack_v2.db
   \\server\apps\TimeTrack\Backups\
   ```

### For Individual Users

1. **Extract TimeTrack folder anywhere:**
   - Desktop
   - `C:\Apps\TimeTrack\`
   - USB drive
   - Network folder

2. **Run `TimeTrack.exe` from that location**

3. **Database and logs created automatically in same folder**

---

## ?? Troubleshooting

### Database Not Being Created

**Check permissions:**
```powershell
# Can you write to the folder?
$testFile = "$(Split-Path (Get-Process -Name TimeTrack).Path)\test.txt"
"test" | Out-File $testFile
Remove-Item $testFile
```

**Check path:**
```powershell
# Where is the exe running from?
Get-Process -Name TimeTrack | Select-Object Path
```

### Log File Not Being Created

**Check if folder is read-only:**
```powershell
$exeFolder = Split-Path (Get-Process -Name TimeTrack).Path
(Get-Item $exeFolder).Attributes
```

**Check disk space:**
```powershell
Get-PSDrive C | Select-Object Used, Free
```

---

## ?? Comparison: Old vs New

| Aspect | Pre-.NET 8 (Old) | .NET 8 Before Fix | .NET 8 After Fix (Now) |
|--------|------------------|-------------------|----------------------|
| **Database** | `timetrack.db` beside exe | `%APPDATA%\TimeTrack v2\` | `timetrack_v2.db` beside exe ? |
| **Logs** | N/A or beside exe | `%APPDATA%\TimeTrack v2\` | `time_track_log.txt` beside exe ? |
| **Airlock** | ? No blocking | ? Blocked | ? No blocking |
| **Portable** | ? Yes | ? No | ? Yes |
| **Enterprise** | ? Easy deploy | ? Complex | ? Easy deploy |

---

## ? Migration from AppData Version

### If You Have Existing Data in AppData

**Locate old database:**
```powershell
explorer "$env:APPDATA\TimeTrack v2"
```

**Copy to new location:**
```powershell
# Example: Copy to Desktop
Copy-Item "$env:APPDATA\TimeTrack v2\timetrack_v2.db" "$env:USERPROFILE\Desktop\TimeTrack\" -Force
```

**Run from new location:**
```powershell
cd "$env:USERPROFILE\Desktop\TimeTrack"
.\TimeTrack.exe
# Will use the database you just copied
```

---

## ?? Summary

### ? What This Solves
- **Airlock blocking** - No AppData access = no blocking
- **Portability** - Copy folder anywhere, works immediately
- **Simplicity** - All data in one visible location
- **Enterprise deployment** - Easy to deploy and manage

### ?? Result
Application now works exactly like the pre-.NET 8 version that wasn't blocked by Airlock!

---

**Build Status:** ? Successful  
**Testing:** Ready for work machine  
**Compatibility:** .NET 8 portable mode  
**Airlock:** Should work! ??

---

**Change Date:** 2025-01-XX  
**Modified Files:** `Data/Database.cs`, `Utilities/ErrorHandler.cs`  
**Removed Code:** All AppData migration logic (no longer needed)
