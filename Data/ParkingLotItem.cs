using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TimeTrack.Data
{
    public class ParkingLotItem : INotifyPropertyChanged
    {
        public int Id { get; set; }

        private string _text = string.Empty;
        public string Text
        {
            get => _text;
            set { _text = value; OnPropertyChanged(); }
        }

        public DateTime CreatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }

        public string DisplayTimestamp => CreatedAt.ToString("ddd HH:mm");
        public bool IsResolved => ResolvedAt != null;

        public string DisplayText => IsResolved ? $"[done] {Text}" : Text;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
