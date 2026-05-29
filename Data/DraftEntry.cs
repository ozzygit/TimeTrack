using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TimeTrack.Data
{
    /// <summary>
    /// Represents a parked (in-progress) time entry saved to the database.
    /// When resumed, its ticket/notes pre-fill a new segment form.
    /// </summary>
    public partial class DraftEntry : ObservableObject
    {
        private readonly int _id;
        private readonly DateTime _parkedAt;

        public DraftEntry(int id, string ticketNumber, string notes, string startTime, DateTime parkedAt)
        {
            _id = id;
            _parkedAt = parkedAt;
            TicketNumber = ticketNumber;
            Notes = notes;
            StartTime = startTime;
        }

        public int Id => _id;
        public DateTime ParkedAt => _parkedAt;

        [ObservableProperty]
        private string ticketNumber = string.Empty;

        [ObservableProperty]
        private string notes = string.Empty;

        [ObservableProperty]
        private string startTime = string.Empty;

        public string ParkedAtDisplay => _parkedAt.ToString("hh:mm tt");

        public string TicketDisplay =>
            string.IsNullOrWhiteSpace(TicketNumber) ? "(no ticket)" : TicketNumber;

        public string NotesPreview =>
            string.IsNullOrWhiteSpace(Notes)
                ? "(no notes)"
                : Notes.Length > 40 ? Notes[..40] + "…" : Notes;
    }

    /// <summary>
    /// EF Core entity for the drafts table.
    /// </summary>
    public class DraftEntity
    {
        public int Id { get; set; }
        public string TicketNumber { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string StartTime { get; set; } = string.Empty;
        public string ParkedAt { get; set; } = string.Empty;
    }
}
