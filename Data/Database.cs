using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace TimeTrack.Data;

/// <summary>
/// Database access layer using Entity Framework Core with SQLite
/// </summary>
public static class Database
{
    public const string DateFormat = "yyyy-MM-dd";

    // Use user's Documents\TimeTrack\timetrack.db
    private static readonly string AppFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TimeTrack");
    private static readonly string DatabasePath = Path.Combine(AppFolder, "timetrack.db");

    // Legacy path (next to the executable) for migration support
    private static readonly string LegacyPath = Path.Combine(AppContext.BaseDirectory, "timetrack.db");

    // Backups folder
    private static readonly string BackupsFolder = Path.Combine(AppFolder, "Backups");

    private const int BackupRetentionCount = 7;

    /// <summary>
    /// Ensure the app data folder exists.
    /// </summary>
    private static void EnsureAppFolder()
    {
        if (!Directory.Exists(AppFolder))
        {
            Directory.CreateDirectory(AppFolder);
        }
    }

    /// <summary>
    /// Create a dated backup of the database if it exists. Only one backup per day.
    /// </summary>
    private static void BackupDatabase(string reason)
    {
        try
        {
            if (!File.Exists(DatabasePath)) return;
            Directory.CreateDirectory(BackupsFolder);
            var stamp = DateTime.Now.ToString("yyyyMMdd");
            var target = Path.Combine(BackupsFolder, $"timetrack_{stamp}_{reason}.db");
            if (File.Exists(target)) return; // already backed up today for this reason
            File.Copy(DatabasePath, target, overwrite: false);
            PruneBackups();
        }
        catch (Exception ex)
        {
            Error.Handle("Failed to create database backup.", ex);
        }
    }

    /// <summary>
    /// Keep only the newest N backups.
    /// </summary>
    private static void PruneBackups()
    {
        try
        {
            if (!Directory.Exists(BackupsFolder)) return;
            var files = new DirectoryInfo(BackupsFolder)
                .GetFiles("timetrack_*.db")
                .OrderByDescending(f => f.CreationTimeUtc)
                .ToList();
            foreach (var file in files.Skip(BackupRetentionCount))
            {
                try { file.Delete(); } catch { /* ignore */ }
            }
        }
        catch (Exception ex)
        {
            Error.Handle("Failed to prune old database backups.", ex);
        }
    }

    /// <summary>
    /// Apply recommended SQLite PRAGMAs for reliability.
    /// </summary>
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
            Error.Handle("Failed to apply SQLite PRAGMAs.", ex);
        }
    }

    /// <summary>
    /// Run a lightweight SQLite integrity check. Returns true if OK.
    /// </summary>
    private static bool IntegrityCheck(TimeTrackDbContext context)
    {
        try
        {
            using var conn = context.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
                conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA integrity_check;";
            var result = cmd.ExecuteScalar()?.ToString();
            return string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Error.Handle("Failed to run SQLite integrity check.", ex);
            return false;
        }
    }

    /// <summary>
    /// Seed __EFMigrationsHistory if the table exists but migrations are empty.
    /// </summary>
    private static void BaselineMigrationsIfNeeded(TimeTrackDbContext context)
    {
        try
        {
            var pending = context.Database.GetPendingMigrations();
            var applied = context.Database.GetAppliedMigrations();
            if (applied.Any() || !pending.Any()) return;

            using var conn = context.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
                conn.Open();

            // Check if time_entries table exists
            using (var checkCmd = conn.CreateCommand())
            {
                checkCmd.CommandText = "SELECT COUNT(1) FROM sqlite_master WHERE type='table' AND name='time_entries';";
                var exists = Convert.ToInt32(checkCmd.ExecuteScalar() ?? 0) > 0;
                if (!exists) return; // nothing to baseline
            }

            // Ensure history table exists and seed initial migration if history is empty
            using (var createHistory = conn.CreateCommand())
            {
                createHistory.CommandText = "CREATE TABLE IF NOT EXISTS __EFMigrationsHistory (MigrationId TEXT PRIMARY KEY, ProductVersion TEXT NOT NULL);";
                createHistory.ExecuteNonQuery();
            }
            using (var countHistory = conn.CreateCommand())
            {
                countHistory.CommandText = "SELECT COUNT(1) FROM __EFMigrationsHistory;";
                var count = Convert.ToInt32(countHistory.ExecuteScalar() ?? 0);
                if (count == 0)
                {
                    using var insert = conn.CreateCommand();
                    insert.CommandText = "INSERT OR IGNORE INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES (@m, @v);";
                    var p1 = insert.CreateParameter(); p1.ParameterName = "@m"; p1.Value = "20251018000000_InitialCreate"; insert.Parameters.Add(p1);
                    var p2 = insert.CreateParameter(); p2.ParameterName = "@v"; p2.Value = "8.0.10"; insert.Parameters.Add(p2);
                    insert.ExecuteNonQuery();
                }
            }
        }
        catch (Exception ex)
        {
            Error.Handle("Failed to baseline EF migrations history.", ex);
        }
    }

    /// <summary>
    /// Check if the database file exists (migrates from legacy location if needed)
    /// </summary>
    public static bool Exists()
    {
        try
        {
            EnsureAppFolder();
            if (!File.Exists(DatabasePath) && File.Exists(LegacyPath))
            {
                File.Copy(LegacyPath, DatabasePath, overwrite: true);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            Error.Handle($"No access verifying/migrating DB.\nTarget: {DatabasePath}\nLegacy: {LegacyPath}", ex);
            return false;
        }
        catch (IOException ex)
        {
            Error.Handle($"I/O error verifying/migrating DB.\nTarget: {DatabasePath}\nLegacy: {LegacyPath}", ex);
            return false;
        }
        catch (Exception ex)
        {
            Error.Handle($"Unexpected error verifying/migrating DB.\nTarget: {DatabasePath}\nLegacy: {LegacyPath}", ex);
            return false;
        }
        return File.Exists(DatabasePath);
    }

    /// <summary>
    /// Create or migrate the database and ensure schema is up to date
    /// </summary>
    public static void CreateDatabase()
    {
        try
        {
            EnsureAppFolder();

            if (!File.Exists(DatabasePath) && File.Exists(LegacyPath))
            {
                BackupDatabase("pre-migrate");
                try { File.Move(LegacyPath, DatabasePath); }
                catch (IOException moveEx)
                {
                    try { File.Copy(LegacyPath, DatabasePath, overwrite: true); try { File.Delete(LegacyPath); } catch { } }
                    catch (Exception copyEx) { Error.Handle($"Failed to migrate legacy DB.\nFrom: {LegacyPath}\nTo: {DatabasePath}", new AggregateException(moveEx, copyEx)); throw; }
                }
            }

            // Backup existing DB once per day before touching schema
            BackupDatabase("daily");

            using var context = new TimeTrackDbContext(DatabasePath);

            // Baseline migrations if the database already has tables from EnsureCreated
            BaselineMigrationsIfNeeded(context);

            // Use migrations instead of EnsureCreated
            context.Database.Migrate();

            ApplySqlitePragmas(context);

            // Optional integrity check
            if (!IntegrityCheck(context))
            {
                Error.Handle("SQLite integrity check failed. A backup has been kept.", new InvalidOperationException("PRAGMA integrity_check != ok"));
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            Error.Handle($"No access creating DB at:\n{DatabasePath}", ex);
            throw;
        }
        catch (IOException ex)
        {
            Error.Handle($"I/O error creating DB at:\n{DatabasePath}", ex);
            throw;
        }
        catch (DbUpdateException ex)
        {
            Error.Handle("EF Core update error while ensuring DB schema.", ex);
            throw;
        }
        catch (Exception ex)
        {
            Error.Handle("Unexpected error creating the database.", ex);
            throw;
        }
    }

    /// <summary>
    /// Get the highest ID for a given date
    /// </summary>
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
            Error.Handle("Could not get current entry index.", e);
            throw;
        }
    }

    /// <summary>
    /// Retrieve all time entries for a specific date
    /// </summary>
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
                .ToList();

            // Sort in memory to handle nullable TimeSpan values
            entities = entities
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
            Error.Handle("Something went wrong while retrieving today's entries.", e);
            throw;
        }

        return result;
    }

    /// <summary>
    /// Update or insert multiple time entries
    /// </summary>
    public static void Update(ObservableCollection<TimeEntry> entries)
    {
        if (entries.Count < 1)
            return;

        // Basic validation: duplicates and invalid durations
        try
        {
            var duplicateKeys = entries
                .GroupBy(e => (Date: DateToString(e.Date), e.ID))
                .Where(g => g.Count() > 1)
                .Select(g => $"{g.Key.Date}#{g.Key.ID}")
                .ToList();
            if (duplicateKeys.Any())
            {
                Error.Handle($"Duplicate entry keys detected: {string.Join(", ", duplicateKeys)}", new InvalidOperationException("Duplicate keys"));
                // Continue but may cause DbUpdateException
            }
        }
        catch (Exception ex)
        {
            Error.Handle("Validation error while checking for duplicates.", ex);
        }

        const int maxRetries = 3;
        int attempt = 0;
        while (true)
        {
            try
            {
                using var context = new TimeTrackDbContext(DatabasePath);
                using var tx = context.Database.BeginTransaction();

                foreach (var entry in entries)
                {
                    // Skip invalid negative/zero durations when both times exist and case is empty (non-lunch)
                    var start = entry.StartTime;
                    var end = entry.EndTime;
                    if (start.HasValue && end.HasValue && end <= start)
                    {
                        Error.Handle($"Skipping invalid duration for {DateToString(entry.Date)}#{entry.ID}", new InvalidOperationException("End <= Start"));
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
                break; // success
            }
            catch (DbUpdateException dbEx)
            {
                var summary = string.Join(", ", entries.Select(e => $"{DateToString(e.Date)}#{e.ID}"));
                Error.Handle($"Database update failed for entries: {summary}", dbEx);
                break; // do not retry non-SQLite busy/locked here
            }
            catch (SqliteException sqlEx) when (sqlEx.SqliteErrorCode is 5 or 6) // SQLITE_BUSY or SQLITE_LOCKED
            {
                attempt++;
                if (attempt >= maxRetries)
                {
                    Error.Handle("SQLite was busy/locked after multiple attempts during update.", sqlEx);
                    break;
                }
                Thread.Sleep(200 * attempt); // backoff
                continue;
            }
            catch (Exception e)
            {
                Error.Handle("Something went wrong while updating the entries database.\nThe saved records may not be consistent with what is displayed.", e);
                break;
            }
        }
    }

    /// <summary>
    /// Delete a time entry by date and ID
    /// </summary>
    public static void Delete(DateTime date, int id)
    {
        const int maxRetries = 3;
        int attempt = 0;
        while (true)
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
                break;
            }
            catch (DbUpdateException dbEx)
            {
                Error.Handle($"Could not delete the record due to a database update error. Key: {DateToString(date)}#{id}", dbEx);
                break;
            }
            catch (SqliteException sqlEx) when (sqlEx.SqliteErrorCode is 5 or 6) // SQLITE_BUSY or SQLITE_LOCKED
            {
                attempt++;
                if (attempt >= maxRetries)
                {
                    Error.Handle("SQLite was busy/locked after multiple attempts during delete.", sqlEx);
                    break;
                }
                Thread.Sleep(200 * attempt);
                continue;
            }
            catch (Exception e)
            {
                Error.Handle("Could not delete the record from the database.\nThe saved records may not be consistent with what is displayed.", e);
                break;
            }
        }
    }

    // Helper methods for date/time conversion

    private static string DateToString(DateTime date)
    {
        return date.ToString(DateFormat);
    }

    private static DateTime StringToDate(string str)
    {
        return DateTime.ParseExact(str, DateFormat, DateTimeFormatInfo.InvariantInfo);
    }

    private static string? TimeSpanToString(TimeSpan? timeSpan)
    {
        return timeSpan?.ToString("c");
    }

    private static TimeSpan? StringToTimeSpan(string? str)
    {
        if (string.IsNullOrEmpty(str))
            return null;

        if (TimeSpan.TryParseExact(str, "c", CultureInfo.InvariantCulture, TimeSpanStyles.None, out var result))
            return result;

        return null;
    }

    // --- FIXED: Convert TimeOnly? to TimeSpan? before calling TimeSpanToString ---
    private static string? TimeOnlyToString(TimeOnly? time)
    {
        return time.HasValue ? time.Value.ToTimeSpan().ToString("c") : null;
    }

    private static TimeOnly? StringToTimeOnly(string? str)
    {
        if (string.IsNullOrEmpty(str))
            return null;

        if (TimeSpan.TryParseExact(str, "c", CultureInfo.InvariantCulture, TimeSpanStyles.None, out var ts))
            return TimeOnly.FromTimeSpan(ts);

        return null;
    }

    // Entity conversion methods

    private static TimeEntryEntity TimeEntryToEntity(TimeEntry entry)
    {
        return new TimeEntryEntity
        {
            Date = DateToString(entry.Date),
            Id = entry.ID,
            StartTime = TimeOnlyToString(entry.StartTime), // <-- FIXED
            EndTime = TimeOnlyToString(entry.EndTime),     // <-- FIXED
            CaseNumber = entry.CaseNumber,
            Notes = entry.Notes,
            Recorded = entry.Recorded ? 1 : 0
        };
    }

    private static TimeEntry EntityToTimeEntry(TimeEntryEntity entity)
    {
        var date = StringToDate(entity.Date);
        var startTime = StringToTimeOnly(entity.StartTime); // <-- FIXED
        var endTime = StringToTimeOnly(entity.EndTime);     // <-- FIXED
        var caseNumber = entity.CaseNumber ?? string.Empty;
        var notes = entity.Notes ?? string.Empty;
        var recorded = entity.Recorded != 0;

        return new TimeEntry(date, entity.Id, startTime, endTime, caseNumber, notes, recorded);
    }
}