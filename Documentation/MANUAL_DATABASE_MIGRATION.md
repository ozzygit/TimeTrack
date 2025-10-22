# Manual Database Migration Guide

## Why Manual Migration?

**Automatic migration has been DISABLED** because the act of checking/accessing `%LOCALAPPDATA%` triggers Airlock and other enterprise security software.

The migration code would:
1. Check `Environment.GetFolderPath(SpecialFolder.LocalApplicationData)` - **Airlock blocks this**
2. List files in LocalApplicationData - **Airlock blocks this**
3. Copy files from LocalApplicationData - **Airlock blocks this**

By disabling automatic migration, the application **never touches LocalApplicationData**, avoiding Airlock blocks entirely.

---

## Do You Need to Migrate?

### Check if you have an existing database:

```powershell
# Check OLD location (LocalApplicationData)
Test-Path "$env:LOCALAPPDATA\TimeTrack v2\timetrack_v2.db"

# Check NEW location (ApplicationData/Roaming)
Test-Path "$env:APPDATA\TimeTrack v2\timetrack_v2.db"
```

**If you get:**
- `True` for OLD, `False` for NEW ? **You need to migrate** (see below)
- `False` for OLD, `True` for NEW ? **Already migrated or new install** ?
- `False` for both ? **New user, no action needed** ?
- `True` for both ? **Already migrated, consider deleting old** ?

---

## Manual Migration Steps

### Option 1: PowerShell Script (Recommended)

Copy and run this script:

```powershell
# Migrate-TimeTrackDatabase.ps1

Write-Host "=== TimeTrack Manual Database Migration ===" -ForegroundColor Cyan

$oldLocation = "$env:LOCALAPPDATA\TimeTrack v2"
$newLocation = "$env:APPDATA\TimeTrack v2"

# Check if old database exists
if (-not (Test-Path "$oldLocation\timetrack_v2.db")) {
    Write-Host "? No database found at old location:" -ForegroundColor Red
    Write-Host "   $oldLocation" -ForegroundColor Gray
    Write-Host "`n? You don't need to migrate - just run the app!" -ForegroundColor Green
    exit 0
}

# Check if new database already exists
if (Test-Path "$newLocation\timetrack_v2.db") {
    Write-Host "? Database already exists at new location:" -ForegroundColor Green
    Write-Host "   $newLocation" -ForegroundColor Gray
    Write-Host "`n?? Skipping migration to avoid overwriting existing data" -ForegroundColor Yellow
    
    $viewOld = Read-Host "`nDo you want to compare the two databases? (Y/N)"
    if ($viewOld -eq 'Y' -or $viewOld -eq 'y') {
        Write-Host "`nOLD database:" -ForegroundColor Cyan
        $oldFile = Get-Item "$oldLocation\timetrack_v2.db"
        Write-Host "  Size: $($oldFile.Length) bytes" -ForegroundColor White
        Write-Host "  Modified: $($oldFile.LastWriteTime)" -ForegroundColor White
        
        Write-Host "`nNEW database:" -ForegroundColor Cyan
        $newFile = Get-Item "$newLocation\timetrack_v2.db"
        Write-Host "  Size: $($newFile.Length) bytes" -ForegroundColor White
        Write-Host "  Modified: $($newFile.LastWriteTime)" -ForegroundColor White
    }
    
    exit 0
}

# Perform migration
Write-Host "?? Migrating database..." -ForegroundColor Yellow
Write-Host "  FROM: $oldLocation" -ForegroundColor Gray
Write-Host "  TO:   $newLocation" -ForegroundColor Gray

try {
    # Create new directory
    if (-not (Test-Path $newLocation)) {
        New-Item -ItemType Directory -Path $newLocation -Force | Out-Null
        Write-Host "? Created new directory" -ForegroundColor Green
    }
    
    # Copy database file
    Copy-Item "$oldLocation\timetrack_v2.db" "$newLocation\timetrack_v2.db" -Force
    Write-Host "? Database file copied" -ForegroundColor Green
    
    # Copy backups folder if it exists
    if (Test-Path "$oldLocation\Backups") {
        Copy-Item "$oldLocation\Backups" "$newLocation\Backups" -Recurse -Force
        Write-Host "? Backup files copied" -ForegroundColor Green
    }
    
    # Copy log file if it exists
    if (Test-Path "$oldLocation\time_track_log.txt") {
        Copy-Item "$oldLocation\time_track_log.txt" "$newLocation\time_track_log.txt" -Force
        Write-Host "? Log file copied" -ForegroundColor Green
    }
    
    Write-Host "`n? Migration completed successfully!" -ForegroundColor Green
    
    # Verify
    Write-Host "`n?? Verification:" -ForegroundColor Cyan
    $newDb = Get-Item "$newLocation\timetrack_v2.db"
    Write-Host "  New database: $($newDb.Length) bytes" -ForegroundColor White
    Write-Host "  Location: $($newDb.FullName)" -ForegroundColor White
    
    # Ask about deleting old
    Write-Host ""
    $delete = Read-Host "Delete old database location? (Y/N)"
    if ($delete -eq 'Y' -or $delete -eq 'y') {
        Remove-Item -Path $oldLocation -Recurse -Force
        Write-Host "? Old database folder deleted" -ForegroundColor Green
    } else {
        Write-Host "?? Old database folder kept at: $oldLocation" -ForegroundColor Yellow
        Write-Host "   You can delete it manually after verifying the new location works." -ForegroundColor Gray
    }
    
} catch {
    Write-Host "`n? Migration failed: $_" -ForegroundColor Red
    Write-Host "`nTry manual copy:" -ForegroundColor Yellow
    Write-Host "  1. Open File Explorer" -ForegroundColor White
    Write-Host "  2. Navigate to: $oldLocation" -ForegroundColor White
    Write-Host "  3. Copy all files" -ForegroundColor White
    Write-Host "  4. Paste to: $newLocation" -ForegroundColor White
}

Write-Host "`n? You can now run TimeTrack!" -ForegroundColor Green
```

**Save as `Migrate-TimeTrackDatabase.ps1` and run it.**

---

### Option 2: File Explorer (Manual Copy)

1. **Open File Explorer**

2. **Navigate to OLD location:**
   - Press `Win + R`
   - Type: `%LOCALAPPDATA%\TimeTrack v2`
   - Press Enter

3. **Copy all files** (Ctrl+A, Ctrl+C)

4. **Navigate to NEW location:**
   - Press `Win + R`
   - Type: `%APPDATA%\TimeTrack v2`
   - Press Enter
   - If folder doesn't exist, create it

5. **Paste files** (Ctrl+V)

6. **Verify the database file is there:**
   - Look for `timetrack_v2.db`
   - Check the file size (should match old location)

7. **Optional: Delete old location**
   - After verifying TimeTrack works from new location
   - Delete `%LOCALAPPDATA%\TimeTrack v2` folder

---

## After Migration

### Run TimeTrack

```powershell
# The app will now use the new location automatically
.\TimeTrack.exe
```

### Verify It Works

1. Open TimeTrack
2. Check that your time entries are there
3. Add a new entry to confirm database is writable
4. Close and reopen to confirm changes persist

### Check the Location

```powershell
# View database location in About dialog
# Or check manually:
explorer "$env:APPDATA\TimeTrack v2"
```

---

## Troubleshooting

### "Database is empty after migration"

**Cause:** You may have migrated the wrong file or an old backup.

**Solution:**
```powershell
# Check file sizes
$oldDb = Get-Item "$env:LOCALAPPDATA\TimeTrack v2\timetrack_v2.db" -ErrorAction SilentlyContinue
$newDb = Get-Item "$env:APPDATA\TimeTrack v2\timetrack_v2.db" -ErrorAction SilentlyContinue

Write-Host "OLD: $($oldDb.Length) bytes, Modified: $($oldDb.LastWriteTime)"
Write-Host "NEW: $($newDb.Length) bytes, Modified: $($newDb.LastWriteTime)"

# If sizes don't match, recopy
Copy-Item "$env:LOCALAPPDATA\TimeTrack v2\timetrack_v2.db" "$env:APPDATA\TimeTrack v2\timetrack_v2.db" -Force
```

### "Access Denied" errors

**Cause:** Permissions issue or file is locked.

**Solution:**
1. Close TimeTrack completely
2. Check Task Manager for `TimeTrack.exe` processes
3. Try copying again
4. If still fails, copy to Desktop first, then move to new location

### "Can't find old database"

**Cause:** You may be a new user or database was already migrated.

**Solution:** No migration needed! Just run TimeTrack and it will create a new database.

---

## For IT Administrators

### Deploy Pre-Migrated Database

If deploying to multiple users:

```powershell
# Create a deployment script
$source = "\\share\TimeTrackTemplate\timetrack_v2.db"
$destination = "$env:APPDATA\TimeTrack v2"

if (-not (Test-Path $destination)) {
    New-Item -ItemType Directory -Path $destination -Force
}

Copy-Item $source "$destination\timetrack_v2.db" -Force
```

### Use Custom Location (Bypass Migration)

Set environment variable to use a custom location:

```powershell
# System-wide (requires admin)
[System.Environment]::SetEnvironmentVariable('TIMETRACK_APPDATA', 'D:\TimeTrack', 'Machine')

# Per-user
[System.Environment]::SetEnvironmentVariable('TIMETRACK_APPDATA', 'D:\TimeTrack', 'User')
```

This completely bypasses the Roaming AppData location.

---

## Why This Change Was Made

### The Problem
- Automatic migration accessed `LocalApplicationData`
- Airlock blocks access to `LocalApplicationData`
- **Even checking if a folder exists** triggers Airlock
- This caused the entire application to fail on startup

### The Solution
- **Never touch LocalApplicationData** in the code
- Provide manual migration instructions instead
- Application works immediately on first run
- No Airlock triggers

### The Trade-off
- **Pro:** Application is never blocked by Airlock ?
- **Pro:** Startup is faster (no migration check) ?
- **Pro:** More predictable behavior ?
- **Con:** Users must manually migrate existing databases ??

For **new users**, this is **not a problem** - they get a fresh database in the right location.

For **existing users**, migration is a **one-time manual step** that takes 30 seconds.

---

## Summary

? **New users:** No action required - just run the app!  
?? **Existing users:** Run the migration script once  
?? **Airlock:** Application never touches LocalApplicationData, so no blocking  

**Migration Script:** See "Option 1" above  
**Manual Migration:** See "Option 2" above  

---

**Last Updated:** 2025-01-XX  
**Reason:** Disabled automatic migration to prevent Airlock blocking
