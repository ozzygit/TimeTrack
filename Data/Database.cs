using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using TimeTrack.Utilities;

namespace TimeTrack.Data;

/// <summary>
/// Raw SQL database access for TimeTrack.
/// </summary>
public static class Database
{
    public const string DateFormat = "yyyy-MM-dd";

    private static string GetAppFolder()
    {
        // Check for explicit override via environment variable
        var overridePath = Environment.GetEnvironmentVariable("TIMETRACK_APPDATA");
        if (!string.IsNullOrWhiteSpace(overridePath))
            return overridePath;

        // PORTABLE MODE: Store database in same folder as executable
        return AppDomain.CurrentDomain.BaseDirectory;
    }

    private static readonly string DatabaseFileName = "timetrack_v2.db";
    private static string DatabasePath => Path.Combine(GetAppFolder(), DatabaseFileName);
    private static string BackupFolder => Path.Combine(GetAppFolder(), "Backups");

    /// <summary>Gets the full path to the database file.</summary>
    public static string GetDatabasePath() => DatabasePath;

    /// <summary>Gets the directory containing the database file.</summary>
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

    private static SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection($"Data Source={DatabasePath}");
        conn.Open();
        foreach (var pragma in new[] {
            "PRAGMA foreign_keys=ON",
            "PRAGMA journal_mode=WAL",
            "PRAGMA synchronous=NORMAL",
            "PRAGMA busy_timeout=5000"
        })
        {
            using var p = conn.CreateCommand();
            p.CommandText = pragma;
            p.ExecuteNonQuery();
        }
        return conn;
    }

    /// <summary>
    /// Create a backup of the database if one hasn't been created today.
    /// </summary>
    public static void BackupDatabaseIfNeeded()
    {
        try
        {
            var dbPath = DatabasePath;
            if (!File.Exists(dbPath))
                return;

            EnsureBackupFolder();

            var today = DateTime.Today.ToString("yyyy-MM-dd");
            var backupPath = Path.Combine(BackupFolder, $"timetrack_v2_backup_{today}.db");

            if (File.Exists(backupPath))
            {
                System.Diagnostics.Debug.WriteLine($"Backup already exists for today: {backupPath}");
                return;
            }

            File.Copy(dbPath, backupPath, overwrite: false);
            System.Diagnostics.Debug.WriteLine($"Database backed up to: {backupPath}");
            CleanupOldBackups(5);
        }
        catch (Exception ex)
        {
            try { ErrorHandler.Handle("Failed to backup database.", ex); }
            catch (Exception logEx) { System.Diagnostics.Debug.WriteLine($"Failed to log backup error: {logEx.Message}"); }
        }
    }

    public class BackupInfo
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public long SizeBytes { get; set; }
        public string DisplayDate => Date.ToString("ddd, dd MMM yyyy · HH:mm");
        public string DisplaySize => SizeBytes switch
        {
            < 1024 => $"{SizeBytes} B",
            < 1024 * 1024 => $"{SizeBytes / 1024.0:F1} KB",
            _ => $"{SizeBytes / (1024.0 * 1024):F1} MB"
        };
    }

    /// <summary>Lists all available backup files, newest first.</summary>
    public static List<BackupInfo> GetBackups()
    {
        var list = new List<BackupInfo>();
        try
        {
            if (!Directory.Exists(BackupFolder))
                return list;

            foreach (var file in Directory.GetFiles(BackupFolder, "timetrack_v2_backup_*.db"))
            {
                var info = new FileInfo(file);
                list.Add(new BackupInfo
                {
                    FileName = info.Name,
                    FilePath = info.FullName,
                    Date = info.CreationTime,
                    SizeBytes = info.Length
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to list backups: {ex.Message}");
        }
        return list.OrderByDescending(b => b.Date).ToList();
    }

    /// <summary>Creates a backup immediately, regardless of whether one exists for today.</summary>
    public static string CreateBackupNow()
    {
        try
        {
            var dbPath = DatabasePath;
            if (!File.Exists(dbPath))
                throw new FileNotFoundException("Database file not found.", dbPath);

            EnsureBackupFolder();

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
            var backupPath = Path.Combine(BackupFolder, $"timetrack_v2_backup_{timestamp}.db");
            File.Copy(dbPath, backupPath, overwrite: true);
            CleanupOldBackups(5);
            return backupPath;
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle("Failed to create backup.", ex);
            throw;
        }
    }

    /// <summary>Restores the database from a backup file. Returns true on success.</summary>
    public static bool RestoreFromBackup(string backupFilePath)
    {
        try
        {
            if (!File.Exists(backupFilePath))
                throw new FileNotFoundException("Backup file not found.", backupFilePath);

            var dbPath = DatabasePath;

            // Checkpoint WAL and clear connection pool so the file is unlocked
            FlushAndClearPool();

            // Pre-restore safety backup of current database
            if (File.Exists(dbPath))
            {
                var preRestoreBackup = Path.Combine(BackupFolder,
                    $"timetrack_v2_prerestore_{DateTime.Now:yyyy-MM-dd_HHmmss}.db");
                EnsureBackupFolder();
                File.Copy(dbPath, preRestoreBackup, overwrite: true);
            }

            // WAL mode may have -wal and -shm files that should be removed
            foreach (var ext in new[] { "", "-wal", "-shm" })
            {
                var sidecar = dbPath + ext;
                if (File.Exists(sidecar))
                    File.Delete(sidecar);
            }

            File.Copy(backupFilePath, dbPath, overwrite: true);
            System.Diagnostics.Debug.WriteLine($"Database restored from: {backupFilePath}");
            return true;
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle("Failed to restore database from backup.", ex);
            return false;
        }
    }

    /// <summary>Imports a database file from an external location. Returns true on success.</summary>
    public static bool ImportDatabase(string sourceFilePath)
    {
        try
        {
            if (!File.Exists(sourceFilePath))
                throw new FileNotFoundException("Source database file not found.", sourceFilePath);

            var dbPath = DatabasePath;

            // Checkpoint WAL and clear connection pool so the file is unlocked
            FlushAndClearPool();

            // Pre-import safety backup
            if (File.Exists(dbPath))
            {
                EnsureBackupFolder();
                var preImportBackup = Path.Combine(BackupFolder,
                    $"timetrack_v2_preimport_{DateTime.Now:yyyy-MM-dd_HHmmss}.db");
                File.Copy(dbPath, preImportBackup, overwrite: true);
            }

            // Remove WAL/shm sidecars
            foreach (var ext in new[] { "", "-wal", "-shm" })
            {
                var sidecar = dbPath + ext;
                if (File.Exists(sidecar))
                    File.Delete(sidecar);
            }

            File.Copy(sourceFilePath, dbPath, overwrite: true);
            System.Diagnostics.Debug.WriteLine($"Database imported from: {sourceFilePath}");
            return true;
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle("Failed to import database.", ex);
            return false;
        }
    }

    /// <summary>Exports the current database to an external location. Returns true on success.</summary>
    public static bool ExportDatabase(string destinationPath)
    {
        try
        {
            var dbPath = DatabasePath;
            if (!File.Exists(dbPath))
                throw new FileNotFoundException("Database file not found.", dbPath);

            File.Copy(dbPath, destinationPath, overwrite: true);
            System.Diagnostics.Debug.WriteLine($"Database exported to: {destinationPath}");
            return true;
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle("Failed to export database.", ex);
            return false;
        }
    }

    /// <summary>Gets the backup folder path.</summary>
    public static string GetBackupFolder() => BackupFolder;

    /// <summary>
    /// Checkpoints the WAL file into the main database and clears the connection pool
    /// so the database file is unlocked for copy/delete operations.
    /// </summary>
    private static void FlushAndClearPool()
    {
        try
        {
            using var conn = new SqliteConnection($"Data Source={DatabasePath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE)";
            cmd.ExecuteNonQuery();
            conn.Close();
        }
        catch
        {
            // Connection may already be closed — that's fine
        }
        SqliteConnection.ClearAllPools();
    }

    private static void CleanupOldBackups(int keepCount)
    {
        try
        {
            if (!Directory.Exists(BackupFolder))
                return;

            var backupFiles = Directory.GetFiles(BackupFolder, "timetrack_v2_backup_*.db")
                .Select(f => new { FilePath = f, CreationTime = File.GetCreationTime(f) })
                .OrderByDescending(f => f.CreationTime)
                .ToList();

            foreach (var file in backupFiles.Skip(keepCount))
            {
                File.Delete(file.FilePath);
                System.Diagnostics.Debug.WriteLine($"Deleted old backup: {file.FilePath}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to cleanup old backups: {ex.Message}");
        }
    }

    /// <summary>
    /// Create the database and schema if they do not exist.
    /// </summary>
    public static void CreateDatabase()
    {
        try
        {
            EnsureAppFolder();
            using var conn = OpenConnection();

            Exec(conn, @"CREATE TABLE IF NOT EXISTS time_entries (
                date         TEXT NOT NULL,
                id           INTEGER NOT NULL,
                start_time   TEXT,
                end_time     TEXT,
                case_number  TEXT,
                notes        TEXT,
                recorded     INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (date, id)
            )");
            Exec(conn, "CREATE INDEX IF NOT EXISTS IX_time_entries_date ON time_entries (date)");
            Exec(conn, "CREATE INDEX IF NOT EXISTS IX_time_entries_date_start_end ON time_entries (date, start_time, end_time)");

            Exec(conn, @"CREATE TABLE IF NOT EXISTS drafts (
                id            INTEGER PRIMARY KEY AUTOINCREMENT,
                ticket_number TEXT,
                notes         TEXT,
                start_time    TEXT,
                end_time      TEXT,
                parked_at     TEXT NOT NULL,
                is_active     INTEGER NOT NULL DEFAULT 0
            )");

            // Migrations: add columns that may not exist in older databases
            try { Exec(conn, "ALTER TABLE drafts ADD COLUMN is_active INTEGER NOT NULL DEFAULT 0"); } catch { }
            try { Exec(conn, "ALTER TABLE drafts ADD COLUMN end_time TEXT"); } catch { }
            try { Exec(conn, "ALTER TABLE drafts ADD COLUMN is_timer_running INTEGER NOT NULL DEFAULT 0"); } catch { }
            try { Exec(conn, "ALTER TABLE drafts ADD COLUMN timer_started_at TEXT"); } catch { }
            try { Exec(conn, "ALTER TABLE time_entries ADD COLUMN deleted_at TEXT"); } catch { }

            Exec(conn, @"CREATE TABLE IF NOT EXISTS notes_history (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                draft_id   INTEGER NOT NULL,
                notes      TEXT,
                saved_at   TEXT NOT NULL
            )");
            Exec(conn, "CREATE INDEX IF NOT EXISTS IX_notes_history_draft_id ON notes_history (draft_id, id DESC)");

            Exec(conn, "PRAGMA optimize");
        }
        catch (Exception ex)
        {
            try { ErrorHandler.Handle("Unexpected error creating the database.", ex); }
            catch (Exception logEx) { System.Diagnostics.Debug.WriteLine($"Failed to log error: {logEx.Message}"); }
            throw;
        }
    }

    public static int CurrentIdCount(DateTime date)
    {
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COALESCE(MAX(id), 0) FROM time_entries WHERE date = @date";
            cmd.Parameters.AddWithValue("@date", DateToString(date));
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
        catch (Exception e)
        {
            try { ErrorHandler.Handle("Could not get current entry index.", e); }
            catch (Exception logEx) { System.Diagnostics.Debug.WriteLine($"Failed to log error: {logEx.Message}"); }
            throw;
        }
    }

    public static ObservableCollection<TimeEntry> Retrieve(DateTime date)
    {
        var result = new ObservableCollection<TimeEntry>();
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT date, id, start_time, end_time, case_number, notes, recorded
                FROM time_entries
                WHERE date = @date AND deleted_at IS NULL
                ORDER BY start_time, end_time, id";
            cmd.Parameters.AddWithValue("@date", DateToString(date));

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                result.Add(ReadTimeEntry(reader));
        }
        catch (Exception e)
        {
            try { ErrorHandler.Handle("Something went wrong while retrieving today's entries.", e); }
            catch (Exception logEx) { System.Diagnostics.Debug.WriteLine($"Failed to log error: {logEx.Message}"); }
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
                using var conn = OpenConnection();
                using var tx = conn.BeginTransaction();

                foreach (var entry in entries)
                {
                    if (!entry.StartTime.HasValue || !entry.EndTime.HasValue)
                    {
                        System.Diagnostics.Debug.WriteLine($"Skipping entry with missing times for {DateToString(entry.Date)}#{entry.ID}");
                        continue;
                    }

                    using var cmd = conn.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
                        INSERT OR REPLACE INTO time_entries (date, id, start_time, end_time, case_number, notes, recorded)
                        VALUES (@date, @id, @start, @end, @case, @notes, @recorded)";
                    cmd.Parameters.AddWithValue("@date", DateToString(entry.Date));
                    cmd.Parameters.AddWithValue("@id", entry.ID);
                    cmd.Parameters.AddWithValue("@start", (object?)TimeOnlyToString(entry.StartTime) ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@end", (object?)TimeOnlyToString(entry.EndTime) ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@case", (object?)entry.TicketNumber ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@notes", (object?)entry.Notes ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@recorded", entry.Recorded ? 1 : 0);
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
                return;
            }
            catch (SqliteException sqlEx) when (sqlEx.SqliteErrorCode is 5 or 6)
            {
                if (attempt >= maxRetries - 1)
                {
                    try { ErrorHandler.Handle("SQLite was busy/locked after multiple attempts during update.", sqlEx); }
                    catch (Exception logEx) { System.Diagnostics.Debug.WriteLine($"Failed to log error: {logEx.Message}"); }
                    return;
                }
                System.Threading.Thread.Sleep(200 * (attempt + 1));
            }
            catch (Exception e)
            {
                try { ErrorHandler.Handle("Something went wrong while updating the entries database.", e); }
                catch (Exception logEx) { System.Diagnostics.Debug.WriteLine($"Failed to log error: {logEx.Message}"); }
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
                using var conn = OpenConnection();
                using var tx = conn.BeginTransaction();
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = "UPDATE time_entries SET deleted_at = @deletedAt WHERE date = @date AND id = @id";
                cmd.Parameters.AddWithValue("@deletedAt", DateTime.Now.ToString("o"));
                cmd.Parameters.AddWithValue("@date", DateToString(date));
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
                tx.Commit();
                return;
            }
            catch (SqliteException sqlEx) when (sqlEx.SqliteErrorCode is 5 or 6)
            {
                if (attempt >= maxRetries - 1)
                {
                    try { ErrorHandler.Handle("SQLite was busy/locked after multiple attempts during delete.", sqlEx); }
                    catch (Exception logEx) { System.Diagnostics.Debug.WriteLine($"Failed to log error: {logEx.Message}"); }
                    return;
                }
                System.Threading.Thread.Sleep(200 * (attempt + 1));
            }
            catch (Exception e)
            {
                try { ErrorHandler.Handle("Could not delete the record from the database.", e); }
                catch (Exception logEx) { System.Diagnostics.Debug.WriteLine($"Failed to log error: {logEx.Message}"); }
                return;
            }
        }
    }

    public class DeletedEntryInfo
    {
        public string Date { get; set; } = string.Empty;
        public int Id { get; set; }
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public string TicketNumber { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public DateTime DeletedAt { get; set; }
        public string DisplayDate => Date;
        public string DisplayTimeRange => $"{StartTime}–{EndTime}";
        public string DisplayDeletedAt => DeletedAt.ToString("ddd, dd MMM yyyy · HH:mm");
        public string NotesPreview => Notes.Length > 60 ? Notes[..60] + "…" : (string.IsNullOrEmpty(Notes) ? "(no notes)" : Notes);
    }

    /// <summary>Lists all soft-deleted entries, newest deletion first.</summary>
    public static List<DeletedEntryInfo> GetDeletedEntries()
    {
        var list = new List<DeletedEntryInfo>();
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT date, id, start_time, end_time, case_number, notes, deleted_at
                FROM time_entries
                WHERE deleted_at IS NOT NULL
                ORDER BY deleted_at DESC";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new DeletedEntryInfo
                {
                    Date = reader.GetString(0),
                    Id = reader.GetInt32(1),
                    StartTime = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    EndTime = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    TicketNumber = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    Notes = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    DeletedAt = DateTime.Parse(reader.GetString(6), null, System.Globalization.DateTimeStyles.RoundtripKind)
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to get deleted entries: {ex.Message}");
        }
        return list;
    }

    /// <summary>Restores a soft-deleted entry.</summary>
    public static bool RestoreDeletedEntry(string date, int id)
    {
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE time_entries SET deleted_at = NULL WHERE date = @date AND id = @id";
            cmd.Parameters.AddWithValue("@date", date);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to restore deleted entry: {ex.Message}");
            return false;
        }
    }

    /// <summary>Permanently deletes a soft-deleted entry.</summary>
    public static bool PurgeDeletedEntry(string date, int id)
    {
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM time_entries WHERE date = @date AND id = @id AND deleted_at IS NOT NULL";
            cmd.Parameters.AddWithValue("@date", date);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to purge deleted entry: {ex.Message}");
            return false;
        }
    }

    /// <summary>Permanently deletes all soft-deleted entries.</summary>
    public static int PurgeAllDeleted()
    {
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM time_entries WHERE deleted_at IS NOT NULL";
            return cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to purge all deleted entries: {ex.Message}");
            return 0;
        }
    }

    /// <summary>Permanently deletes soft-deleted entries older than the specified days.</summary>
    public static int PurgeOldDeletedEntries(int retentionDays = 90)
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-retentionDays).ToString("o");
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM time_entries WHERE deleted_at IS NOT NULL AND deleted_at < @cutoff";
            cmd.Parameters.AddWithValue("@cutoff", cutoff);
            return cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to purge old deleted entries: {ex.Message}");
            return 0;
        }
    }

    public static ObservableCollection<DraftEntry> RetrieveDrafts()
    {
        var result = new ObservableCollection<DraftEntry>();
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, ticket_number, notes, start_time, end_time, is_active, is_timer_running, timer_started_at FROM drafts ORDER BY parked_at";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                result.Add(ReadDraftEntry(reader));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to retrieve drafts: {ex.Message}");
        }
        return result;
    }

    public static DraftEntry? SaveDraft(string ticketNumber, string notes, string startTime, string endTime = "", bool isActive = false)
    {
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO drafts (ticket_number, notes, start_time, end_time, parked_at, is_active, is_timer_running, timer_started_at)
                VALUES (@ticket, @notes, @start, @end, @parkedAt, @active, @timerRunning, @timerStartedAt);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@ticket", ticketNumber);
            cmd.Parameters.AddWithValue("@notes", notes);
            cmd.Parameters.AddWithValue("@start", startTime);
            cmd.Parameters.AddWithValue("@end", endTime);
            cmd.Parameters.AddWithValue("@parkedAt", DateTime.Now.ToString("o"));
            cmd.Parameters.AddWithValue("@active", isActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@timerRunning", 0);
            cmd.Parameters.AddWithValue("@timerStartedAt", string.Empty);
            var newId = Convert.ToInt32(cmd.ExecuteScalar());
            return new DraftEntry(newId, ticketNumber, notes, startTime, endTime, isActive);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save draft: {ex.Message}");
            return null;
        }
    }

    public static void UpdateDraft(DraftEntry draft)
    {
        try
        {
            using var conn = OpenConnection();

            // Log previous notes to history if they changed
            string? previousNotes = null;
            using (var readCmd = conn.CreateCommand())
            {
                readCmd.CommandText = "SELECT notes FROM drafts WHERE id = @id";
                readCmd.Parameters.AddWithValue("@id", draft.Id);
                var result = readCmd.ExecuteScalar();
                previousNotes = result as string;
            }

            if (previousNotes != draft.Notes)
            {
                using var histCmd = conn.CreateCommand();
                histCmd.CommandText = "INSERT INTO notes_history (draft_id, notes, saved_at) VALUES (@draftId, @notes, @savedAt)";
                histCmd.Parameters.AddWithValue("@draftId", draft.Id);
                histCmd.Parameters.AddWithValue("@notes", previousNotes ?? string.Empty);
                histCmd.Parameters.AddWithValue("@savedAt", DateTime.Now.ToString("o"));
                histCmd.ExecuteNonQuery();
            }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE drafts
                SET ticket_number = @ticket, notes = @notes, start_time = @start,
                    end_time = @end, is_active = @active, is_timer_running = @timerRunning, timer_started_at = @timerStartedAt
                WHERE id = @id";
            cmd.Parameters.AddWithValue("@ticket", draft.TicketNumber);
            cmd.Parameters.AddWithValue("@notes", draft.Notes);
            cmd.Parameters.AddWithValue("@start", draft.StartTime);
            cmd.Parameters.AddWithValue("@end", draft.EndTime);
            cmd.Parameters.AddWithValue("@active", draft.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@timerRunning", draft.IsTimerRunning ? 1 : 0);
            cmd.Parameters.AddWithValue("@timerStartedAt", draft.TimerStartedAt);
            cmd.Parameters.AddWithValue("@id", draft.Id);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to update draft {draft.Id}: {ex.Message}");
        }
    }

    public static void DeleteDraft(int id)
    {
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM drafts WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to delete draft {id}: {ex.Message}");
        }
    }

    public class NotesHistoryRecord
    {
        public int Id { get; set; }
        public string Notes { get; set; } = string.Empty;
        public DateTime SavedAt { get; set; }
        public string DisplayTime => SavedAt.ToString("ddd, dd MMM yyyy HH:mm");
        public string Preview => Notes.Length > 80 ? Notes[..80] + "…" : Notes;
    }

    /// <summary>Gets notes history for a draft, newest first.</summary>
    public static List<NotesHistoryRecord> GetNotesHistory(int draftId)
    {
        var list = new List<NotesHistoryRecord>();
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, notes, saved_at FROM notes_history WHERE draft_id = @draftId ORDER BY id DESC";
            cmd.Parameters.AddWithValue("@draftId", draftId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new NotesHistoryRecord
                {
                    Id = reader.GetInt32(0),
                    Notes = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    SavedAt = DateTime.Parse(reader.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind)
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to get notes history: {ex.Message}");
        }
        return list;
    }

    /// <summary>Gets the most recent notes history entry for a draft, or null if none.</summary>
    public static NotesHistoryRecord? GetLastNotesHistory(int draftId)
    {
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, notes, saved_at FROM notes_history WHERE draft_id = @draftId ORDER BY id DESC LIMIT 1";
            cmd.Parameters.AddWithValue("@draftId", draftId);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new NotesHistoryRecord
                {
                    Id = reader.GetInt32(0),
                    Notes = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    SavedAt = DateTime.Parse(reader.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind)
                };
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to get last notes history: {ex.Message}");
        }
        return null;
    }

    /// <summary>Deletes a notes history record after it has been restored (to prevent undo loops).</summary>
    public static void RemoveNotesHistoryRecord(int historyId)
    {
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM notes_history WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", historyId);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to remove notes history record: {ex.Message}");
        }
    }

    /// <summary>Updates notes directly on the drafts table without logging to history.</summary>
    public static void UpdateDraftNotesDirect(int draftId, string notes)
    {
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE drafts SET notes = @notes WHERE id = @id";
            cmd.Parameters.AddWithValue("@notes", notes);
            cmd.Parameters.AddWithValue("@id", draftId);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to update draft notes directly: {ex.Message}");
        }
    }

    private static void Exec(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static TimeEntry ReadTimeEntry(SqliteDataReader r)
    {
        var date = StringToDate(r.GetString(0));
        var id = r.GetInt32(1);
        var startTime = StringToTimeOnly(r.IsDBNull(2) ? null : r.GetString(2));
        var endTime = StringToTimeOnly(r.IsDBNull(3) ? null : r.GetString(3));
        var ticketNumber = r.IsDBNull(4) ? string.Empty : r.GetString(4);
        var notes = r.IsDBNull(5) ? string.Empty : r.GetString(5);
        var recorded = !r.IsDBNull(6) && r.GetInt32(6) != 0;
        return new TimeEntry(date, id, startTime, endTime, ticketNumber, notes, recorded);
    }

    private static DraftEntry ReadDraftEntry(SqliteDataReader r)
    {
        var id = r.GetInt32(0);
        var ticket = r.IsDBNull(1) ? string.Empty : r.GetString(1);
        var notes = r.IsDBNull(2) ? string.Empty : r.GetString(2);
        var start = r.IsDBNull(3) ? string.Empty : r.GetString(3);
        var end = r.IsDBNull(4) ? string.Empty : r.GetString(4);
        var isActive = !r.IsDBNull(5) && r.GetInt32(5) != 0;
        var isTimerRunning = !r.IsDBNull(6) && r.GetInt32(6) != 0;
        var timerStartedAt = r.IsDBNull(7) ? string.Empty : r.GetString(7);
        var draft = new DraftEntry(id, ticket, notes, start, end, isActive);
        draft.IsTimerRunning = isTimerRunning;
        draft.TimerStartedAt = timerStartedAt;
        return draft;
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
}