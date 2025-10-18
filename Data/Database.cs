using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
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
    /// Check if the database file exists (migrates from legacy location if needed)
    /// </summary>
    public static bool Exists()
    {
        try
        {
            EnsureAppFolder();

            if (!File.Exists(DatabasePath) && File.Exists(LegacyPath))
            {
                // Migrate legacy DB beside exe to Documents folder
                File.Copy(LegacyPath, DatabasePath, overwrite: true);
            }
        }
        catch (Exception e)
        {
            Error.Handle("Could not verify or migrate the database path.", e);
        }

        return File.Exists(DatabasePath);
    }

    /// <summary>
    /// Create the database and ensure schema is up to date
    /// </summary>
    public static void CreateDatabase()
    {
        try
        {
            EnsureAppFolder();

            // Migrate legacy DB beside the EXE to AppData if needed
            if (!File.Exists(DatabasePath) && File.Exists(LegacyPath))
            {
                // Prefer move; fall back to copy+delete if cross-volume or locked scenarios arise
                try
                {
                    File.Move(LegacyPath, DatabasePath);
                }
                catch (IOException)
                {
                    File.Copy(LegacyPath, DatabasePath, overwrite: true);
                    try { File.Delete(LegacyPath); } catch { /* ignore delete failure */ }
                }
            }

            using var context = new TimeTrackDbContext(DatabasePath);
            context.Database.EnsureCreated();
        }
        catch (Exception e)
        {
            Error.Handle("Could not create the database.", e);
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

        try
        {
            using var context = new TimeTrackDbContext(DatabasePath);

            foreach (var entry in entries)
            {
                var dateString = DateToString(entry.Date);
                var entity = TimeEntryToEntity(entry);

                var existing = context.TimeEntries
                    .FirstOrDefault(e => e.Date == dateString && e.Id == entry.ID);

                if (existing != null)
                {
                    // Update existing entry
                    context.Entry(existing).CurrentValues.SetValues(entity);
                }
                else
                {
                    // Add new entry
                    context.TimeEntries.Add(entity);
                }
            }

            context.SaveChanges();
        }
        catch (Exception e)
        {
            Error.Handle("Something went wrong while updating the entries database.\nThe saved records may not be consistent with what is displayed.", e);
        }
    }

    /// <summary>
    /// Delete a time entry by date and ID
    /// </summary>
    public static void Delete(DateTime date, int id)
    {
        try
        {
            using var context = new TimeTrackDbContext(DatabasePath);
            var dateString = DateToString(date);

            var entity = context.TimeEntries
                .FirstOrDefault(e => e.Date == dateString && e.Id == id);

            if (entity != null)
            {
                context.TimeEntries.Remove(entity);
                context.SaveChanges();
            }
        }
        catch (Exception e)
        {
            Error.Handle("Could not delete the record from the database.\nThe saved records may not be consistent with what is displayed.", e);
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