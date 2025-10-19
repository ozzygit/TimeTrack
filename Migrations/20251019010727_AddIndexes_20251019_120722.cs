using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTrack.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexes_20251019_120722 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_time_entries_date",
                table: "time_entries",
                column: "date");

            migrationBuilder.CreateIndex(
                name: "IX_time_entries_date_start_end",
                table: "time_entries",
                columns: new[] { "date", "start_time", "end_time" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_time_entries_date",
                table: "time_entries");

            migrationBuilder.DropIndex(
                name: "IX_time_entries_date_start_end",
                table: "time_entries");
        }
    }
}
