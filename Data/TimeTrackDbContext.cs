using System.IO;
using Microsoft.EntityFrameworkCore;

namespace TimeTrack.Data;

/// <summary>
/// Entity Framework Core DbContext for TimeTrack database
/// </summary>
public class TimeTrackDbContext : DbContext
{
    private const string DefaultDbFileName = "timetrack.db";
    private readonly string _dbPath;

    public TimeTrackDbContext(string dbPath = DefaultDbFileName)
    {
        // If a directory is passed, append the default file name
        if (string.IsNullOrWhiteSpace(dbPath))
            dbPath = DefaultDbFileName;

        // Treat paths without extension as directories
        bool looksLikeDirectory = !Path.HasExtension(dbPath);
        if (looksLikeDirectory)
            dbPath = Path.Combine(dbPath, DefaultDbFileName);

        _dbPath = dbPath;
    }

    public DbSet<TimeEntryEntity> TimeEntries { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // Enable shared cache and pooling; set default timeout for commands
            optionsBuilder.UseSqlite($"Data Source={_dbPath};Cache=Shared;Pooling=True;Default Timeout=5");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TimeEntryEntity>(entity =>
        {
            entity.ToTable("time_entries");
            
            // Composite primary key
            entity.HasKey(e => new { e.Date, e.Id });
            
            entity.Property(e => e.Date)
                .HasColumnName("date")
                .IsRequired();
            
            entity.Property(e => e.Id)
                .HasColumnName("id")
                .IsRequired();
            
            entity.Property(e => e.StartTime)
                .HasColumnName("start_time");
            
            entity.Property(e => e.EndTime)
                .HasColumnName("end_time");
            
            entity.Property(e => e.CaseNumber)
                .HasColumnName("case_number")
                .HasMaxLength(255);
            
            entity.Property(e => e.Notes)
                .HasColumnName("notes");
            
            entity.Property(e => e.Recorded)
                .HasColumnName("recorded")
                .IsRequired();
        });

        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>
/// Entity class representing a time entry in the database
/// </summary>
public class TimeEntryEntity
{
    public string Date { get; set; } = string.Empty;
    public int Id { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public string? CaseNumber { get; set; }
    public string? Notes { get; set; }
    public int Recorded { get; set; }
}