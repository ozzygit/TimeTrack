using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TimeTrack.Data;

#nullable disable

namespace TimeTrack.Data.Migrations
{
    [DbContext(typeof(TimeTrackDbContext))]
    public partial class TimeTrackDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.10");

            modelBuilder.Entity("TimeTrack.Data.TimeEntryEntity", b =>
            {
                b.Property<string>("Date")
                    .HasColumnType("TEXT")
                    .HasColumnName("date");

                b.Property<int>("Id")
                    .HasColumnType("INTEGER")
                    .HasColumnName("id");

                b.Property<string>("CaseNumber")
                    .HasMaxLength(255)
                    .HasColumnType("TEXT")
                    .HasColumnName("case_number");

                b.Property<string>("EndTime")
                    .HasColumnType("TEXT")
                    .HasColumnName("end_time");

                b.Property<string>("Notes")
                    .HasColumnType("TEXT")
                    .HasColumnName("notes");

                b.Property<int>("Recorded")
                    .HasColumnType("INTEGER")
                    .HasColumnName("recorded");

                b.Property<string>("StartTime")
                    .HasColumnType("TEXT")
                    .HasColumnName("start_time");

                b.HasKey("Date", "Id");

                b.ToTable("time_entries", (string)null);
            });
#pragma warning restore 612, 618
        }
    }
}
