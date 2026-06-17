using System;
using System.Collections.Generic;
using System.Linq;

namespace CyberInfo.Engine
{
    /// <summary>
    /// Stores a timestamped history of all significant bot actions.
    /// Supports paged retrieval (Task 4 requirement).
    /// </summary>
    public class ActivityLogger
    {
        private readonly List<(DateTime Timestamp, string Action)> _logs = new();
        private const int MaxStoredEntries = 500;

        /// <summary>Total number of stored log entries.</summary>
        public int TotalCount => _logs.Count;

        /// <summary>Records a new log entry with the current timestamp.</summary>
        public void Log(string action)
        {
            _logs.Add((DateTime.Now, action));
            // Trim to max to avoid unlimited growth
            if (_logs.Count > MaxStoredEntries)
                _logs.RemoveAt(0);
        }

        /// <summary>Returns the most recent <paramref name="count"/> entries, newest first.</summary>
        public List<string> GetRecentLogs(int count = 10)
        {
            return _logs
                .TakeLast(count)
                .Reverse()
                .Select((l, idx) => $"{idx + 1}. [{l.Timestamp:HH:mm:ss}]  {l.Action}")
                .ToList();
        }

        /// <summary>Returns all log entries, newest first.</summary>
        public List<string> GetAllLogs()
        {
            return _logs
                .AsEnumerable()
                .Reverse()
                .Select((l, idx) => $"{idx + 1}. [{l.Timestamp:yyyy-MM-dd HH:mm:ss}]  {l.Action}")
                .ToList();
        }
    }
}
