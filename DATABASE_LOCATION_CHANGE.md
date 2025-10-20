# Database Location Change

## Summary
The TimeTrack database location has been moved from `%LOCALAPPDATA%` to `%APPDATA%\Roaming` to resolve compatibility issues with security software (e.g., Airlock) and improve multi-profile support.

## Changes

### Old Location
```
C:\Users\{username}\AppData\Local\TimeTrack v2\timetrack_v2.db
```

### New Location
```
C:\Users\{username}\AppData\Roaming\TimeTrack v2\timetrack_v2.db
```

## Migration

When you first run the updated version, TimeTrack will:
1. Check for an existing database in the old location
2. **Automatically migrate** your database to the new location
3. Keep a backup in the old location (you can delete it after verifying everything works)

**No manual action is required!**

## Why This Change?

1. **Security Software Compatibility**: Some enterprise security tools (like Airlock) restrict access to LocalAppData folders
2. **OneDrive Profiles**: Better handling of multiple OneDrive accounts with different Documents folders
3. **Standard Practice**: ApplicationData (Roaming) is the Microsoft-recommended location for application data
4. **Consistency**: Works reliably across different Windows configurations

## For IT Administrators

### Custom Database Location
You can specify a custom database location using the `TIMETRACK_APPDATA` environment variable:

```powershell
# System-wide
[System.Environment]::SetEnvironmentVariable('TIMETRACK_APPDATA', 'D:\CompanyData\TimeTrack', 'Machine')

# Per-user
[System.Environment]::SetEnvironmentVariable('TIMETRACK_APPDATA', 'D:\MyData\TimeTrack', 'User')
```

### Group Policy Deployment
For enterprise deployments, you can:
1. Set `TIMETRACK_APPDATA` via Group Policy
2. Pre-deploy the database to the specified location
3. Use a network share for centralized storage (though local storage is recommended for performance)

### Manual Migration (if needed)
If automatic migration fails or you need to manually migrate:

```powershell
# 1. Copy the database
Copy-Item "$env:LOCALAPPDATA\TimeTrack v2\*" "$env:APPDATA\TimeTrack v2\" -Recurse

# 2. Verify the new location has the database
Test-Path "$env:APPDATA\TimeTrack v2\timetrack_v2.db"

# 3. After verifying, optionally remove old location
Remove-Item "$env:LOCALAPPDATA\TimeTrack v2" -Recurse
```

## Troubleshooting

### Database Not Found After Update
If you see a "database not found" error after updating:

1. Check both locations for your database:
   ```powershell
   dir "$env:LOCALAPPDATA\TimeTrack v2\timetrack_v2.db"
   dir "$env:APPDATA\TimeTrack v2\timetrack_v2.db"
   ```

2. If it's only in the old location, manually copy it:
   ```powershell
   xcopy "$env:LOCALAPPDATA\TimeTrack v2" "$env:APPDATA\TimeTrack v2" /E /I
   ```

3. Restart TimeTrack

### Permission Issues
If you get permission errors:
1. Run TimeTrack as your normal user (don't use "Run as Administrator" unless necessary)
2. Check that you have write permissions to `%APPDATA%`
3. Contact your IT administrator if permissions are restricted

### OneDrive Sync Issues
The new location (`%APPDATA%\Roaming`) is **not synced by OneDrive** by default, which is intentional:
- Prevents sync conflicts between machines
- Avoids database corruption from concurrent access
- Better performance (local-only access)

If you need to sync your database:
1. Use the `TIMETRACK_APPDATA` environment variable to point to a OneDrive folder
2. **WARNING**: Only do this if you use TimeTrack on one machine at a time
3. SQLite databases can corrupt if accessed simultaneously from multiple machines

## Rollback

If you need to rollback to the old location:

```powershell
# Set environment variable to use old location
[System.Environment]::SetEnvironmentVariable('TIMETRACK_APPDATA', "$env:LOCALAPPDATA", 'User')

# Copy database back if needed
Copy-Item "$env:APPDATA\TimeTrack v2\*" "$env:LOCALAPPDATA\TimeTrack v2\" -Recurse

# Restart TimeTrack
```

## Support

If you encounter issues with the database migration:
1. Check the application logs in the new database folder
2. Create a GitHub issue with:
   - Your Windows version
   - Whether you're using OneDrive
   - Any security software you're running
   - Error messages or screenshots

---

**Change Date**: 2025-01-XX  
**Applies to**: TimeTrack v2.1.1+
