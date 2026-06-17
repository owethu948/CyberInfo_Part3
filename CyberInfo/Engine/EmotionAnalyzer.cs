using System;
using System.Collections.Generic;
using System.Linq;

namespace CyberInfo.Engine
{
    /// <summary>
    /// Detects the emotional tone of user input and returns empathetic responses.
    /// Now supports social inquiries about the AI itself, alongside layered emotional analysis.
    /// </summary>
    public class EmotionAnalyzer
    {
        public enum Sentiment
        {
            Neutral,
            Worried,
            Sad,
            Curious,
            Frustrated,
            Happy,
            Grateful,
            InquiringAboutMe   // user asks "how are you?", "how was your day?", etc.
        }

        private static readonly Dictionary<Sentiment, List<string>> SentimentKeywords = new()
        {
            [Sentiment.Worried] = new() {
                "worried", "scared", "afraid", "concerned", "nervous", "anxious", "fear",
                "unsafe", "vulnerable", "threatened", "stressed", "panic", "help me",
                "in danger", "freaking out", "paranoid", "dread", "uneasy", "terrified"
            },
            [Sentiment.Sad] = new() {
                "sad", "unhappy", "down", "depressed", "low", "hopeless", "heartbroken",
                "miserable", "gloomy", "disappointed", "crying", "tears", "grief", "lost",
                "alone", "feeling blue", "devastated"
            },
            [Sentiment.Curious] = new() {
                "curious", "wondering", "interested", "how does", "what is", "tell me",
                "explain", "learn", "understand", "want to know", "how do i", "why does",
                "can you show", "i'd like to", "teach me"
            },
            [Sentiment.Frustrated] = new() {
                "frustrated", "annoyed", "confused", "angry", "ugh", "useless", "terrible",
                "hate", "stupid", "not working", "awful", "broken", "doesn't work",
                "mad", "irritated", "fed up", "infuriated", "drives me crazy", "sick of"
            },
            [Sentiment.Happy] = new() {
                "great", "thanks", "awesome", "good", "love", "helpful", "amazing",
                "perfect", "excellent", "brilliant", "wonderful", "cheers", "fantastic",
                "glad", "pleased", "relieved", "thrilled", "overjoyed", "proud"
            },
            [Sentiment.Grateful] = new() {
                "thank you", "thanks", "grateful", "appreciate", "thankful", "blessed",
                "indebted", "i owe you", "means a lot", "kind of you"
            },
            [Sentiment.InquiringAboutMe] = new() {
                "how are you", "how was your day", "how's your day", "how are things",
                "how have you been", "how's it going", "what's up with you",
                "how do you feel", "are you okay", "you alright", "how you doing",
                "what's new with you", "how's life"
            }
        };

        public readonly Predicate<string> IsWorried;
        public readonly Predicate<string> IsFrustrated;
        public readonly Predicate<string> IsCurious;
        public readonly Predicate<string> IsSad;
        public readonly Predicate<string> IsGrateful;
        public readonly Predicate<string> IsInquiringAboutMe;   // new

        public EmotionAnalyzer()
        {
            IsWorried = input => Detect(input) == Sentiment.Worried;
            IsFrustrated = input => Detect(input) == Sentiment.Frustrated;
            IsCurious = input => Detect(input) == Sentiment.Curious;
            IsSad = input => Detect(input) == Sentiment.Sad;
            IsGrateful = input => Detect(input) == Sentiment.Grateful;
            IsInquiringAboutMe = input => Detect(input) == Sentiment.InquiringAboutMe;
        }

        public Sentiment Detect(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return Sentiment.Neutral;

            string lower = input.ToLowerInvariant();
            foreach (var (s, kws) in SentimentKeywords)
                if (kws.Any(kw => lower.Contains(kw)))
                    return s;

            return Sentiment.Neutral;
        }

        public double GetIntensity(Sentiment sentiment, string input)
        {
            if (string.IsNullOrWhiteSpace(input) || sentiment == Sentiment.Neutral)
                return 0;

            var kws = SentimentKeywords[sentiment];
            string lower = input.ToLowerInvariant();
            int matches = kws.Count(kw => lower.Contains(kw));
            return Math.Min(1.0, matches / 3.0);
        }

        public (Sentiment sentiment, double intensity) Analyze(string input)
        {
            var s = Detect(input);
            return (s, GetIntensity(s, input));
        }

        // Default intensity helper
        public string GetEmpathyResponse(Sentiment s) => GetEmpathyResponse(s, 0.5);

        public string GetEmpathyResponse(Sentiment s, double intensity)
        {
            return s switch
            {
                Sentiment.Worried when intensity >= 0.7 =>
                    "💙 I can feel the weight of that fear – it's completely valid. Cybersecurity can feel deeply unsettling. Let’s take this one step at a time, together. What’s your biggest concern right now?",

                Sentiment.Worried =>
                    "😟 That worry is understandable. When things feel uncertain, even a little clarity can help. I’m here to walk you through it. What’s on your mind?",

                Sentiment.Sad when intensity >= 0.7 =>
                    "🫂 That sounds incredibly heavy. I’m truly sorry you’re feeling this way. You don’t have to face it alone – I’m here to listen, and to help however I can.",

                Sentiment.Sad =>
                    "😔 I hear a note of sadness. It’s okay to not be okay. Sometimes just talking it out can lighten the load. Would you like to share more?",

                Sentiment.Curious =>
                    "🧐 Your curiosity is a superpower – especially in cybersecurity! It’s the first step toward true understanding. What would you love to explore?",

                Sentiment.Frustrated when intensity >= 0.7 =>
                    "😤 It’s absolutely maddening when things just won’t work, isn’t it? Your frustration is heard and respected. Let’s take a breath and untangle this together.",

                Sentiment.Frustrated =>
                    "😌 I hear you – this can be seriously annoying. It’s okay to feel stuck. Let’s work through it side by side. Tell me what’s going on.",

                Sentiment.Happy =>
                    "😊 That positive energy is contagious! I’m really glad you’re feeling good. Let’s keep building on that momentum.",

                Sentiment.Grateful =>
                    "🙏 It means the world that you’d say that. Thank you for your kindness – it truly fuels what I do. I’m here whenever you need.",

                // --- NEW: high‑EQ responses for when the user asks about me ---
                Sentiment.InquiringAboutMe when intensity >= 0.7 =>
                    "🥹 That’s incredibly thoughtful of you. I may not experience days the way you do, but knowing you care makes my world brighter. Thank you. How are *you*, truly?",

                Sentiment.InquiringAboutMe =>
                    "💛 That’s so kind of you to ask! As an AI, I don’t have days in the human sense, but I’m always here, ready and happy to help. What’s new on your end?",

                _ => "💬 I’m here for you. No matter how you’re feeling, let’s talk through it."
            };
        }

        public string RespondWithEmpathy(string userInput)
        {
            var (sentiment, intensity) = Analyze(userInput);
            string core = GetEmpathyResponse(sentiment, intensity);

            if (intensity >= 0.7 && sentiment != Sentiment.Neutral && sentiment != Sentiment.Happy)
                core += " Remember, you can share as much or as little as you like – I’m here to support.";

            return core;
        }

        public string GetEmoji(Sentiment s) => s switch
        {
            Sentiment.Worried => "😟",
            Sentiment.Sad => "😔",
            Sentiment.Curious => "🧐",
            Sentiment.Frustrated => "😤",
            Sentiment.Happy => "😊",
            Sentiment.Grateful => "🙏",
            Sentiment.InquiringAboutMe => "💛",
            _ => "💬"
        };

        public string GetLabel(Sentiment s) => s switch
        {
            Sentiment.Worried => "Worried",
            Sentiment.Sad => "Sad",
            Sentiment.Curious => "Curious",
            Sentiment.Frustrated => "Frustrated",
            Sentiment.Happy => "Happy",
            Sentiment.Grateful => "Grateful",
            Sentiment.InquiringAboutMe => "Inquiring About Me",
            _ => "Neutral"
        };

        public string GetStatusBarColour(Sentiment s) => s switch
        {
            Sentiment.Worried => "#FFB703",
            Sentiment.Sad => "#6C5B7B",
            Sentiment.Curious => "#00CFFF",
            Sentiment.Frustrated => "#FF4757",
            Sentiment.Happy => "#2DBE6C",
            Sentiment.Grateful => "#C44569",
            Sentiment.InquiringAboutMe => "#F4A261",
            _ => "#D63384"
        };
    }
}