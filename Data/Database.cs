using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TimeTrack.Utilities;

namespace TimeTrack.Data;

/// <summary>
/// Simplified database access: create the application's DB if it does not exist.
/// </summary>
public static class Database
{
    public const string DateFormat = "yyyy-MM-dd";

    private static string GetAppFolder()
    {
        var overridePath = Environment.GetEnvironmentVariable("TIMETRACK_APPDATA");
        if (!string.IsNullOrWhiteSpace(overridePath))
            return Path.Combine(overridePath, "TimeTrack v2");

        // Changed from LocalApplicationData to ApplicationData (Roaming) to avoid Airlock blocking
        // This folder typically resides at: %APPDATA%\TimeTrack v2
        // Benefits:
        // - Not blocked by security software like Airlock
        // - Works consistently across OneDrive profiles
        // - Standard location for application data
        // - Can still be overridden via TIMETRACK_APPDATA environment variable
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TimeTrack v2");
    }

    private static readonly string DatabaseFileName = "timetrack_v2.db";
    private static string DatabasePath => Path.Combine(GetAppFolder(), DatabaseFileName);
    private static string BackupFolder => Path.Combine(GetAppFolder(), "Backups");

    /// <summary>
    /// Get the full path to the database file.
    /// </summary>
    public static string GetDatabasePath() => DatabasePath;

    /// <summary>
    /// Get the directory containing the database file.
    /// </summary>
    public static string GetDatabaseDirectory() => GetAppFolder();

    private static void EnsureAppFolder()
    {
        var appFolder = GetAppFolder();
        if (!Directory.Exists(appFolder))
            Directory.CreateDirectory(appFolder);
    }

    private static void EnsureBackupFolder()
    {
        if (!Directory.Exists(BackupFolder))
            Directory.CreateDirectory(BackupFolder);
    }

    private static void ApplySqlitePragmas(TimeTrackDbContext context)
    {
        try
        {
            context.Database.ExecuteSqlRaw("PRAGMA foreign_keys=ON;");
            context.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
            context.Database.ExecuteSqlRaw("PRAGMA synchronous=NORMAL;");
            context.Database.ExecuteSqlRaw("PRAGMA busy_timeout=5000;");
            context.Database.ExecuteSqlRaw("PRAGMA optimize;");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to apply SQLite pragmas: {ex.Message}");
        }
    }

    /// <summary>
    /// Create a backup of the database if one hasn't been created today.
    /// </summary>
    public static void BackupDatabaseIfNeeded()
    {
        try
        {
            var dbPath = DatabasePath;
            
            // Skip if database doesn't exist yet
            if (!File.Exists(dbPath))
                return;

            EnsureBackupFolder();

            // Check if a backup already exists for today
            var today = DateTime.Today.ToString("yyyy-MM-dd");
            var backupFileName = $"timetrack_v2_backup_{today}.db";
            var backupPath = Path.Combine(BackupFolder, backupFileName);

            if (File.Exists(backupPath))
            {
                System.Diagnostics.Debug.WriteLine($"Backup already exists for today: {backupPath}");
                return;
            }

            // Create the backup
            File.Copy(dbPath, backupPath, overwrite: false);
            System.Diagnostics.Debug.WriteLine($"Database backed up to: {backupPath}");

            // Clean up old backups (keep last 5 copies)
            CleanupOldBackups(5);
        }
        catch (Exception ex)
        {
            try
            {
                ErrorHandler.Handle("Failed to backup database.", ex);
            }
            catch (Exception logEx)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to log backup error: {logEx.Message}");
            }
        }
    }

    /// <summary>
    /// Remove old backup files, keeping only the specified number of most recent backups.
    /// </summary>
    private static void CleanupOldBackups(int keepCount)
    {
        try
        {
            if (!Directory.Exists(BackupFolder))
                return;

            var backupFiles = Directory.GetFiles(BackupFolder, "timetrack_v2_backup_*.db")
                .Select(filePath => new
                {
                    FilePath = filePath,
                    FileName = Path.GetFileNameWithoutExtension(filePath),
                    CreationTime = File.GetCreationTime(filePath)
                })
                .OrderByDescending(f => f.CreationTime)
                .ToList();

            // If we have more backups than we want to keep, delete the oldest ones
            if (backupFiles.Count > keepCount)
            {
                var filesToDelete = backupFiles.Skip(keepCount);
                
                foreach (var fileInfo in filesToDelete)
                {
                    File.Delete(fileInfo.FilePath);
                    System.Diagnostics.Debug.WriteLine($"Deleted old backup: {fileInfo.FilePath}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to cleanup old backups: {ex.Message}");
        }
    }

    /// <summary>
    /// Create the database if it does not exist.
    /// </summary>
    public static void CreateDatabase()
    {
        try
        {
            EnsureAppFolder();
            
            // Attempt to migrate from old location if database doesn't exist in new location
            MigrateFromOldLocationIfNeeded();

            using var context = new TimeTrackDbContext(DatabasePath);
            context.Database.EnsureCreated();
            ApplySqlitePragmas(context);
        }
        catch (Exception ex)
        {
            try 
            { 
                ErrorHandler.Handle("Unexpected error creating the database.", ex); 
            } 
            catch (Exception logEx)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to log error: {logEx.Message}");
            }
            throw;
        }
    }

    /// <summary>
    /// Migrate database from old LocalApplicationData location to new ApplicationData location.
    /// DISABLED: This migration code can trigger Airlock blocking.
    /// Users who need migration should use manual migration instructions.
    /// </summary>
    private static void MigrateFromOldLocationIfNeeded()
    {
        // MIGRATION DISABLED TO AVOID AIRLOCK BLOCKING
        // The act of checking LocalApplicationData can trigger security software
        
        // Skip if database already exists in new location
        if (File.Exists(DatabasePath))
        {
            System.Diagnostics.Debug.WriteLine("Database already exists in new location, skipping migration.");
            return;
        }

        System.Diagnostics.Debug.WriteLine("Automatic migration is disabled. Database will be created in new location.");
        System.Diagnostics.Debug.WriteLine("If you have an existing database, please use manual migration:");
        System.Diagnostics.Debug.WriteLine($"  Copy from: %LOCALAPPDATA%\\TimeTrack v2\\");
        System.Diagnostics.Debug.WriteLine($"  Copy to: {GetAppFolder()}");
        
        // Do NOT access LocalApplicationData - it triggers Airlock
        
        /* ORIGINAL MIGRATION CODE - DISABLED
        try
        {
            // Skip if database already exists in new location
            if (File.Exists(DatabasePath))
            {
                System.Diagnostics.Debug.WriteLine("Database already exists in new location, skipping migration.");
                return;
            }

            // Check for database in old location (LocalApplicationData)
            var oldAppFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                "TimeTrack v2");
            var oldDatabasePath = Path.Combine(oldAppFolder, DatabaseFileName);

            if (!File.Exists(oldDatabasePath))
            {
                System.Diagnostics.Debug.WriteLine("No database found in old location, nothing to migrate.");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"Migrating database from old location: {oldDatabasePath}");
            System.Diagnostics.Debug.WriteLine($"New location: {DatabasePath}");

            // Ensure new folder exists
            EnsureAppFolder();

            // Copy database file
            File.Copy(oldDatabasePath, DatabasePath, overwrite: false);
            System.Diagnostics.Debug.WriteLine("Database file migrated successfully.");

            // Copy backups folder if it exists
            var oldBackupFolder = Path.Combine(oldAppFolder, "Backups");
            if (Directory.Exists(oldBackupFolder))
            {
                EnsureBackupFolder();
                var newBackupFolder = BackupFolder;

                foreach (var backupFile in Directory.GetFiles(oldBackupFolder, "*.db"))
                {
                    var fileName = Path.GetFileName(backupFile);
                    var newBackupPath = Path.Combine(newBackupFolder, fileName);
                    File.Copy(backupFile, newBackupPath, overwrite: false);
                }
                System.Diagnostics.Debug.WriteLine("Backup files migrated successfully.");
            }

            // Copy any other files in the old folder
            foreach (var file in Directory.GetFiles(oldAppFolder))
            {
                var fileName = Path.GetFileName(file);
                if (fileName != DatabaseFileName && !fileName.StartsWith("timetrack_v2_backup_"))
                {
                    var newFilePath = Path.Combine(GetAppFolder(), fileName);
                    File.Copy(file, newFilePath, overwrite: false);
                    System.Diagnostics.Debug.WriteLine($"Migrated file: {fileName}");
                }
            }

            MessageBox.Show(
                $"Your TimeTrack database has been automatically migrated to a new location:\n\n" +
                $"Old: {oldAppFolder}\n" +
                $"New: {GetAppFolder()}\n\n" +
                $"This change resolves compatibility issues with security software.\n" +
                $"You can safely delete the old folder after verifying everything works correctly.",
                "Database Migration Completed",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            System.Diagnostics.Debug.WriteLine("Migration completed successfully.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Database migration failed: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            
            // Don't throw - allow app to continue even if migration fails
            // User can manually migrate if needed
            MessageBox.Show(
                $"Automatic database migration encountered an issue:\n{ex.Message}\n\n" +
                $"Please see DATABASE_LOCATION_CHANGE.md for manual migration instructions.",
                "Migration Warning",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        */
    }

    public static int CurrentIdCount(DateTime date)
    {
        try
        {
            using var context = new TimeTrackDbContext(DatabasePath);
            var dateString = DateToString(date);

            var maxId = context.TimeEntries
                .Where(e => e.Date == dateString)
                .Max(e => (int?)e.Id);

            return maxId ?? 0;
        }
        catch (Exception e)
        {
            try 
            { 
                ErrorHandler.Handle("Could not get current entry index.", e); 
            } 
            catch (Exception logEx)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to log error: {logEx.Message}");
            }
            throw;
        }
    }

    public static ObservableCollection<TimeEntry> Retrieve(DateTime date)
    {
        var result = new ObservableCollection<TimeEntry>();

        try
        {
            using var context = new TimeTrackDbContext(DatabasePath);
            var dateString = DateToString(date);

            var entities = context.TimeEntries
                .Where(e => e.Date == dateString)
                .AsNoTracking()
                .OrderBy(e => e.StartTime)
                .ThenBy(e => e.EndTime)
                .ThenBy(e => e.Id)
                .ToList();

            foreach (var entity in entities)
            {
                var entry = EntityToTimeEntry(entity);
                result.Add(entry);
            }
        }
        catch (Exception e)
        {
            try 
            { 
                ErrorHandler.Handle("Something went wrong while retrieving today's entries.", e); 
            } 
            catch (Exception logEx)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to log error: {logEx.Message}");
            }
            throw;
        }

        return result;
    }

    public static void Update(ObservableCollection<TimeEntry> entries)
    {
        if (entries.Count < 1) return;

        const int maxRetries = 3;
        
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                using var context = new TimeTrackDbContext(DatabasePath);
                using var tx = context.Database.BeginTransaction();

                foreach (var entry in entries)
                {
                    var start = entry.StartTime;
                    var end = entry.EndTime;
                    
                    // Skip validation - allow equal times (0 duration) and let overnight shifts be handled by Duration property
                    // Only skip entries with no times set
                    if (!start.HasValue || !end.HasValue)
                    {
                        System.Diagnostics.Debug.WriteLine($"Skipping entry with missing times for {DateToString(entry.Date)}#{entry.ID}");
                        continue;
                    }

                    var dateString = DateToString(entry.Date);
                    var entity = TimeEntryToEntity(entry);

                    var existing = context.TimeEntries
                        .FirstOrDefault(e => e.Date == dateString && e.Id == entry.ID);

                    if (existing != null)
                    {
                        context.Entry(existing).CurrentValues.SetValues(entity);
                    }
                    else
                    {
                        context.TimeEntries.Add(entity);
                    }
                }

                context.SaveChanges();
                tx.Commit();
                return; // Success
            }
            catch (DbUpdateException dbEx)
            {
                try 
                { 
                    ErrorHandler.Handle("Database update failed.", dbEx); 
                } 
                catch (Exception logEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to log error: {logEx.Message}");
                }
                return;
            }
            catch (SqliteException sqlEx) when (sqlEx.SqliteErrorCode is 5 or 6)
            {
                if (attempt >= maxRetries - 1)
                {
                    try 
                    { 
                        ErrorHandler.Handle("SQLite was busy/locked after multiple attempts during update.", sqlEx); 
                    } 
                    catch (Exception logEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to log error: {logEx.Message}");
                    }
                    return;
                }
                
                // Use Task.Delay for async wait - wrapped in Task.Run for sync context
                Task.Delay(200 * (attempt + 1)).Wait();
            }
            catch (Exception e)
            {
                try 
                { 
                    ErrorHandler.Handle("Something went wrong while updating the entries database.", e); 
                } 
                catch (Exception logEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to log error: {logEx.Message}");
                }
                return;
            }
        }
    }

    public static void Delete(DateTime date, int id)
    {
        const int maxRetries = 3;
        
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                using var context = new TimeTrackDbContext(DatabasePath);
                using var tx = context.Database.BeginTransaction();
                var dateString = DateToString(date);

                var entity = context.TimeEntries
                    .FirstOrDefault(e => e.Date == dateString && e.Id == id);

                if (entity != null)
                {
                    context.TimeEntries.Remove(entity);
                    context.SaveChanges();
                }

                tx.Commit();
                return; // Success
            }
            catch (DbUpdateException dbEx)
            {
                try 
                { 
                    ErrorHandler.Handle($"Could not delete the record due to a database update error. Key: {DateToString(date)}#{id}", dbEx); 
                } 
                catch (Exception logEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to log error: {logEx.Message}");
                }
                return;
            }
            catch (SqliteException sqlEx) when (sqlEx.SqliteErrorCode is 5 or 6)
            {
                if (attempt >= maxRetries - 1)
                {
                    try 
                    { 
                        ErrorHandler.Handle("SQLite was busy/locked after multiple attempts during delete.", sqlEx); 
                    } 
                    catch (Exception logEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to log error: {logEx.Message}");
                    }
                    return;
                }
                
                Task.Delay(200 * (attempt + 1)).Wait();
            }
            catch (Exception e)
            {
                try 
                { 
                    ErrorHandler.Handle("Could not delete the record from the database.", e); 
                } 
                catch (Exception logEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to log error: {logEx.Message}");
                }
                return;
            }
        }
    }

    private static string DateToString(DateTime date) => date.ToString(DateFormat);
    private static DateTime StringToDate(string str) => DateTime.ParseExact(str, DateFormat, DateTimeFormatInfo.InvariantInfo);
    private static string? TimeOnlyToString(TimeOnly? time) => time.HasValue ? time.Value.ToTimeSpan().ToString("c") : null;
    
    private static TimeOnly? StringToTimeOnly(string? str) 
    { 
        if (string.IsNullOrEmpty(str)) return null; 
        if (TimeSpan.TryParseExact(str, "c", CultureInfo.InvariantCulture, TimeSpanStyles.None, out var ts)) 
            return TimeOnly.FromTimeSpan(ts); 
        return null; 
    }

    private static TimeEntryEntity TimeEntryToEntity(TimeEntry entry) => new()
    {
        Date = DateToString(entry.Date),
        Id = entry.ID,
        StartTime = TimeOnlyToString(entry.StartTime),
        EndTime = TimeOnlyToString(entry.EndTime),
        CaseNumber = entry.TicketNumber,
        Notes = entry.Notes,
        Recorded = entry.Recorded ? 1 : 0
    };

    private static TimeEntry EntityToTimeEntry(TimeEntryEntity entity)
    {
        var date = StringToDate(entity.Date);
        var startTime = StringToTimeOnly(entity.StartTime);
        var endTime = StringToTimeOnly(entity.EndTime);
        var ticketNumber = entity.CaseNumber ?? string.Empty;
        var notes = entity.Notes ?? string.Empty;
        var recorded = entity.Recorded != 0;

        return new TimeEntry(date, entity.Id, startTime, endTime, ticketNumber, notes, recorded);
    }
}