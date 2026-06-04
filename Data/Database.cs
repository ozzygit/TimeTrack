using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
                WHERE date = @date
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
                Task.Delay(200 * (attempt + 1)).Wait();
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
                cmd.CommandText = "DELETE FROM time_entries WHERE date = @date AND id = @id";
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
                Task.Delay(200 * (attempt + 1)).Wait();
            }
            catch (Exception e)
            {
                try { ErrorHandler.Handle("Could not delete the record from the database.", e); }
                catch (Exception logEx) { System.Diagnostics.Debug.WriteLine($"Failed to log error: {logEx.Message}"); }
                return;
            }
        }
    }

    public static ObservableCollection<DraftEntry> RetrieveDrafts()
    {
        var result = new ObservableCollection<DraftEntry>();
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, ticket_number, notes, start_time, end_time, is_active FROM drafts ORDER BY parked_at";
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
                INSERT INTO drafts (ticket_number, notes, start_time, end_time, parked_at, is_active)
                VALUES (@ticket, @notes, @start, @end, @parkedAt, @active);
                SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@ticket", ticketNumber);
            cmd.Parameters.AddWithValue("@notes", notes);
            cmd.Parameters.AddWithValue("@start", startTime);
            cmd.Parameters.AddWithValue("@end", endTime);
            cmd.Parameters.AddWithValue("@parkedAt", DateTime.Now.ToString("o"));
            cmd.Parameters.AddWithValue("@active", isActive ? 1 : 0);
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
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE drafts
                SET ticket_number = @ticket, notes = @notes, start_time = @start,
                    end_time = @end, is_active = @active
                WHERE id = @id";
            cmd.Parameters.AddWithValue("@ticket", draft.TicketNumber);
            cmd.Parameters.AddWithValue("@notes", draft.Notes);
            cmd.Parameters.AddWithValue("@start", draft.StartTime);
            cmd.Parameters.AddWithValue("@end", draft.EndTime);
            cmd.Parameters.AddWithValue("@active", draft.IsActive ? 1 : 0);
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
        return new DraftEntry(id, ticket, notes, start, end, isActive);
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