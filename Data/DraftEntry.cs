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

        public DraftEntry(int id, string ticketNumber, string notes, string startTime, string endTime, bool isActive = false)
        {
            _id = id;
            TicketNumber = ticketNumber;
            Notes = notes;
            StartTime = startTime;
            EndTime = endTime;
            IsActive = isActive;
        }

        public int Id => _id;

        /// <summary>
        /// When non-null, this draft is editing an existing submitted entry (date, id).
        /// When null, this draft is a new entry.
        /// </summary>
        public (DateTime Date, int Id)? EditingEntry { get; set; }

        public bool IsEditing => EditingEntry.HasValue;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TabDisplay))]
        private string ticketNumber = string.Empty;

        [ObservableProperty]
        private string notes = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TabDisplay))]
        private string startTime = string.Empty;

        [ObservableProperty]
        private string endTime = string.Empty;

        [ObservableProperty]
        private bool isActive;

        [ObservableProperty]
        private bool isTimerRunning;

        [ObservableProperty]
        private string timerStartedAt = string.Empty;

        /// <summary>
        /// Short label shown on the tab strip.
        /// </summary>
        public string TabDisplay
        {
            get
            {
                string timeHint = string.Empty;
                if (!string.IsNullOrWhiteSpace(StartTime))
                {
                    int spaceIdx = StartTime.IndexOf(' ');
                    timeHint = " · " + (spaceIdx > 0 ? StartTime[..spaceIdx] : StartTime);
                }

                if (!string.IsNullOrWhiteSpace(TicketNumber))
                {
                    string t = TicketNumber.Length > 16 ? TicketNumber[..16] + "…" : TicketNumber;
                    return (IsEditing ? "✎ " : "") + t + timeHint;
                }
                return (IsEditing ? "✎ " : "") + "New Entry" + timeHint;
            }
        }

        public string TicketDisplay =>
            string.IsNullOrWhiteSpace(TicketNumber) ? "(no ticket)" : TicketNumber;

    }

}
