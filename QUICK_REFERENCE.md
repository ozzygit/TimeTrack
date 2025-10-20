# Quick Reference: Testing TimeTrack from Different Locations

## TL;DR

Your app **should** work from any location because it stores data in `%APPDATA%`, not relative to the executable.

**To diagnose why it doesn't work from `c:\temp`:**

```powershell
# 1. Rebuild with new error handling
dotnet clean
dotnet publish -c Release -r win-x64 --self-contained

# 2. Run automated test
.\Publish-And-Test.ps1

# 3. Read the error dialog that appears
# It will tell you exactly what's wrong!
```

---

## What Changed

? **Enhanced startup error handling in `App.xaml.cs`**
- Shows detailed MessageBox on startup failure
- Includes diagnostic paths and full exception details
- Logs everything to debug output and log file

---

## Quick Commands

### Option 1: Automated Test (Recommended)
```powershell
.\Publish-And-Test.ps1
```

### Option 2: Manual Test
```powershell
dotnet publish -c Release -r win-x64 --self-contained
Copy-Item "bin\Release\net8.0-windows\win-x64\publish\TimeTrack.exe" "C:\temp\" -Force
cd c:\temp
.\TimeTrack.exe
# Watch for error dialog!
```

### Option 3: Detailed Diagnostics
```powershell
.\Test-TimeTrackFromTemp.ps1
```

---

## Where Data is Stored

**Database:**
```
%APPDATA%\TimeTrack v2\timetrack_v2.db
C:\Users\<username>\AppData\Roaming\TimeTrack v2\timetrack_v2.db
```

**Logs:**
```
%LOCALAPPDATA%\TimeTrack v2\time_track_log.txt
C:\Users\<username>\AppData\Local\TimeTrack v2\time_track_log.txt
```

**This does NOT change when you move the executable!**

---

## Common Issues & Quick Fixes

### Issue: "Could not load file or assembly"
**Cause:** Missing SQLite DLL
**Quick Check:**
```powershell
Get-ChildItem $env:TEMP -Recurse -Filter "e_sqlite3.dll" -ErrorAction SilentlyContinue
```
**Fix:** Already configured correctly in `.csproj` ?

### Issue: "Access Denied"
**Cause:** Permissions on `c:\temp`
**Quick Check:**
```powershell
"test" | Out-File "C:\temp\test.txt"
Remove-Item "C:\temp\test.txt"
```
**Fix:** Try different directory or check permissions

### Issue: Process Exits Immediately
**Cause:** Security software or missing DLL
**Quick Check:**
```powershell
Get-EventLog -LogName Application -Source ".NET Runtime" -Newest 5
```
**Fix:** Check antivirus logs, try different directory

---

## Files Created

| File | Purpose |
|------|---------|
| `FIX_APPLIED_STARTUP_DIAGNOSTICS.md` | Summary of changes |
| `RUNNING_FROM_DIFFERENT_LOCATIONS.md` | Complete troubleshooting guide |
| `Publish-And-Test.ps1` | Automated publish & test workflow |
| `Test-TimeTrackFromTemp.ps1` | Detailed diagnostic script |
| `QUICK_REFERENCE.md` | This file |

---

## What You Should See

### ? Success:
- Application window opens
- No error dialogs
- Database created in `%APPDATA%\TimeTrack v2\`

### ? Failure (NOW WITH DETAILS):
```
???????????????????????????????????????
? TimeTrack v2 - Startup Error     [X]?
???????????????????????????????????????
? TimeTrack v2 failed to start.       ?
?                                     ?
? Error: [Specific error here]        ?
?                                     ?
? Diagnostic Information:             ?
? - Executable: C:\temp\              ?
? - Working Directory: C:\temp        ?
? - AppData: C:\Users\...\AppData\... ?
?                                     ?
? Full Exception:                     ?
? [Stack trace]                       ?
?                                     ?
?                 [OK]                ?
???????????????????????????????????????
```

**This error dialog will tell you exactly what's wrong!**

---

## Next Steps

1. **Run the test:**
   ```powershell
   .\Publish-And-Test.ps1
   ```

2. **If it fails, you'll see an error dialog** - read it carefully!

3. **Check the log:**
   ```powershell
   notepad "$env:LOCALAPPDATA\TimeTrack v2\time_track_log.txt"
   ```

4. **Refer to detailed guide:**
   - Open `RUNNING_FROM_DIFFERENT_LOCATIONS.md`
   - Find your specific error
   - Follow the solution steps

---

## Need More Help?

- **Detailed troubleshooting:** `RUNNING_FROM_DIFFERENT_LOCATIONS.md`
- **What changed:** `FIX_APPLIED_STARTUP_DIAGNOSTICS.md`
- **Automated testing:** `.\Publish-And-Test.ps1`
- **Manual diagnostics:** `.\Test-TimeTrackFromTemp.ps1`

---

**Remember:** The new error handling will **show you exactly what's wrong** - no more guessing!
