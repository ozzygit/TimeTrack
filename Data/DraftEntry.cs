using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TimeTrack.Data
{
    /// <summary>
    /// Represents an open (in-progress) time entry saved to the database.
    /// Multiple open entries can exist; only one is focused at a time.
    /// </summary>
    public partial class DraftEntry : ObservableObject
    {
        private readonly int _id;
        private readonly DateTime _createdAt;

        public DraftEntry(int id, string ticketNumber, string notes, string startTime, DateTime createdAt, bool isActive = false)
        {
            _id = id;
            _createdAt = createdAt;
            TicketNumber = ticketNumber;
            Notes = notes;
            StartTime = startTime;
            IsActive = isActive;
        }

        public int Id => _id;
        public DateTime CreatedAt => _createdAt;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TabDisplay))]
        private string ticketNumber = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TabDisplay))]
        private string notes = string.Empty;

        [ObservableProperty]
        private string startTime = string.Empty;

        [ObservableProperty]
        private bool isActive;

        /// <summary>
        /// Short label shown on the tab strip.
        /// </summary>
        public string TabDisplay
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(TicketNumber))
                    return TicketNumber.Length > 16 ? TicketNumber[..16] + "…" : TicketNumber;
                if (!string.IsNullOrWhiteSpace(Notes))
                    return Notes.Length > 16 ? Notes[..16] + "…" : Notes;
                return "New Entry";
            }
        }

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
        public int IsActive { get; set; }
    }
}
