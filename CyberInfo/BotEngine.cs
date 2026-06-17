using System;
using System.Collections.Generic;
using System.Linq;

namespace CyberInfo.Engine
{
    /// <summary>
    /// Detects the emotional tone of user input and returns empathetic responses.
    /// Part of the NLP simulation layer.
    /// </summary>
    public class EmotionAnalyzer
    {
        public enum Sentiment { Neutral, Worried, Curious, Frustrated, Happy }

        private static readonly Dictionary<Sentiment, List<string>> SentimentKeywords = new()
        {
            [Sentiment.Worried]    = new() { "worried", "scared", "afraid", "concerned", "nervous",
                                             "anxious", "fear", "unsafe", "vulnerable", "threatened",
                                             "stressed", "panic", "help me", "in danger" },
            [Sentiment.Curious]    = new() { "curious", "wondering", "interested", "how does",
                                             "what is", "tell me", "explain", "learn", "understand",
                                             "want to know", "how do i", "why does" },
            [Sentiment.Frustrated] = new() { "frustrated", "annoyed", "confused", "angry", "ugh",
                                             "useless", "terrible", "hate", "stupid", "not working",
                                             "awful", "broken", "doesn't work" },
            [Sentiment.Happy]      = new() { "great", "thanks", "awesome", "good", "love", "helpful",
                                             "amazing", "perfect", "excellent", "brilliant", "wonderful",
                                             "thank you", "cheers", "fantastic" }
        };

        // Convenience predicates for external use
        public readonly Predicate<string> IsWorried;
        public readonly Predicate<string> IsFrustrated;
        public readonly Predicate<string> IsCurious;

        public EmotionAnalyzer()
        {
            IsWorried    = input => Detect(input) == Sentiment.Worried;
            IsFrustrated = input => Detect(input) == Sentiment.Frustrated;
            IsCurious    = input => Detect(input) == Sentiment.Curious;
        }

        /// <summary>Returns the dominant sentiment found in <paramref name="input"/>.</summary>
        public Sentiment Detect(string input)
        {
            string lower = input.ToLowerInvariant();
            foreach (var (s, kws) in SentimentKeywords)
                if (kws.Any(kw => lower.Contains(kw)))
                    return s;
            return Sentiment.Neutral;
        }

        public string GetEmpathyResponse(Sentiment s) => s switch
        {
            Sentiment.Worried    => "🤗 It's completely understandable to feel that way — cyber threats are real. Let me help you stay safe.",
            Sentiment.Curious    => "🧐 Love the curiosity! That's exactly the right mindset for cybersecurity.",
            Sentiment.Frustrated => "😌 I hear you — this can feel overwhelming. Let's work through it together.",
            Sentiment.Happy      => "😊 Glad you're feeling positive! Let's keep that momentum.",
            _                    => string.Empty
        };

        public string GetEmoji(Sentiment s) => s switch
        {
            Sentiment.Worried    => "😟",
            Sentiment.Curious    => "🧐",
            Sentiment.Frustrated => "😤",
            Sentiment.Happy      => "😊",
            _                    => "💬"
        };

        public string GetLabel(Sentiment s) => s switch
        {
            Sentiment.Worried    => "Worried",
            Sentiment.Curious    => "Curious",
            Sentiment.Frustrated => "Frustrated",
            Sentiment.Happy      => "Happy",
            _                    => "Neutral"
        };

        public string GetStatusBarColour(Sentiment s) => s switch
        {
            Sentiment.Worried    => "#FFB703",
            Sentiment.Curious    => "#00CFFF",
            Sentiment.Frustrated => "#FF4757",
            Sentiment.Happy      => "#2DBE6C",
            _                    => "#D63384"
        };
    }
}
