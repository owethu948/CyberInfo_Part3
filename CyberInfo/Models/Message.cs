using System;
using System.ComponentModel;

namespace CyberInfo.Models
{
    /// <summary>Represents a single chat message bubble.</summary>
    public class Message : INotifyPropertyChanged
    {
        private string _text = string.Empty;

        public string Sender    { get; init; } = "Bot";
        public DateTime Timestamp { get; init; } = DateTime.Now;
        public string Icon      { get; init; } = string.Empty;

        public string Text
        {
            get => _text;
            set { _text = value; OnPropertyChanged(nameof(Text)); }
        }

        public bool IsUser   => Sender == "User";
        public bool IsBot    => Sender == "Bot";
        public bool IsSystem => Sender == "System";

        public string TimestampText => Timestamp.ToString("HH:mm");

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
