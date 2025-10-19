using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TimeTrack v2");
    }

    private static readonly string DatabaseFileName = "timetrack_v2.db";
    private static string DatabasePath => Path.Combine(GetAppFolder(), DatabaseFileName);

    private static void EnsureAppFolder()
    {
        var appFolder = GetAppFolder();
        if (!Directory.Exists(appFolder))
            Directory.CreateDirectory(appFolder);
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
    /// Create the database if it does not exist.
    /// </summary>
    public static void CreateDatabase()
    {
        try
        {
            EnsureAppFolder();

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
                    
                    if (start.HasValue && end.HasValue && end <= start)
                    {
                        try 
                        { 
                            ErrorHandler.Handle($"Skipping invalid duration for {DateToString(entry.Date)}#{entry.ID}", 
                                new InvalidOperationException("End <= Start")); 
                        } 
                        catch (Exception logEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to log error: {logEx.Message}");
                        }
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
        CaseNumber = entry.CaseNumber,
        Notes = entry.Notes,
        Recorded = entry.Recorded ? 1 : 0
    };

    private static TimeEntry EntityToTimeEntry(TimeEntryEntity entity)
    {
        var date = StringToDate(entity.Date);
        var startTime = StringToTimeOnly(entity.StartTime);
        var endTime = StringToTimeOnly(entity.EndTime);
        var caseNumber = entity.CaseNumber ?? string.Empty;
        var notes = entity.Notes ?? string.Empty;
        var recorded = entity.Recorded != 0;

        return new TimeEntry(date, entity.Id, startTime, endTime, caseNumber, notes, recorded);
    }
}