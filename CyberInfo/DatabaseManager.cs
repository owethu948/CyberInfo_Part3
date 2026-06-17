using System.Collections.Generic;
using System.Linq;

namespace CyberInfo.Engine
{
    /// <summary>
    /// Holds in-session memory about the user: name, last topic, and expressed interests.
    /// Enables personalised responses across the conversation.
    /// </summary>
    public class UserProfile
    {
        public string UserName  { get; set; } = string.Empty;
        public bool NameCaptured => !string.IsNullOrWhiteSpace(UserName);
        public string? LastTopic { get; set; }

        public HashSet<string> Interests { get; } =
            new(System.StringComparer.OrdinalIgnoreCase);

        private readonly Queue<string> _history = new();
        private const int MaxHistory = 20;
        private readonly Dictionary<string, string> _facts = new();

        public void RememberInterest(string topic)
        {
            if (!string.IsNullOrWhiteSpace(topic))
                Interests.Add(topic.Trim());
        }

        public string GetInterestSummary() =>
            Interests.Count > 0 ? string.Join(", ", Interests) : "—";

        public void PushInput(string input)
        {
            if (_history.Count >= MaxHistory) _history.Dequeue();
            _history.Enqueue(input);
        }

        public void StoreFact(string key, string value) => _facts[key] = value;
        public string? GetFact(string key) => _facts.TryGetValue(key, out var v) ? v : null;

        public string GetPersonalisedPrefix()
        {
            if (Interests.Count == 0) return string.Empty;
            return $"As someone interested in {Interests.Last()}, ";
        }
    }
}
