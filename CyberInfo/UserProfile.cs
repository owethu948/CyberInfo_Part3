using System;
using System.Collections.Generic;
using System.Linq;

namespace CyberInfo.Engine
{
    /// <summary>
    /// Maps raw user input to cybersecurity topic keys using a keyword synonym dictionary.
    /// Core component of the NLP simulation layer.
    /// </summary>
    public class ThreatRecognizer
    {
        // Synonym map: topic key → array of trigger words
        private readonly Dictionary<string, string[]> _map = new(StringComparer.OrdinalIgnoreCase)
        {
            ["phishing"]          = new[] { "phish", "phishing", "fake email", "email scam", "smishing", "spam mail", "spear phishing" },
            ["password"]          = new[] { "password", "passwords", "passphrase", "credential", "login", "pass", "pin" },
            ["scam"]              = new[] { "scam", "fraud", "con", "advance fee", "419", "fake job", "too good to be true" },
            ["privacy"]           = new[] { "privacy", "private", "personal data", "tracking", "data leak", "surveillance", "gdpr", "popia" },
            ["malware"]           = new[] { "malware", "virus", "trojan", "ransomware", "spyware", "worm", "infected", "keylogger" },
            ["2fa"]               = new[] { "2fa", "two factor", "two-factor", "mfa", "authenticator", "otp", "one time", "verification code" },
            ["wifi"]              = new[] { "wifi", "wi-fi", "hotspot", "public network", "free wifi", "wireless", "open network" },
            ["social engineering"] = new[] { "social engineering", "vishing", "pretexting", "impersonation", "manipulate", "baiting" },
            ["updates"]           = new[] { "update", "patch", "software update", "windows update", "outdated", "firmware" },
            ["safe browsing"]     = new[] { "safe browsing", "https", "browser security", "suspicious link", "malicious site" },
            ["backup"]            = new[] { "backup", "back up", "restore", "recovery", "cloud backup" },
            ["encryption"]        = new[] { "encrypt", "encryption", "decrypt", "cipher", "vpn", "tls", "ssl" }
        };

        private readonly Dictionary<string, string> _toName = new(StringComparer.OrdinalIgnoreCase)
        {
            ["phishing"]          = "Phishing",
            ["password"]          = "Passwords",
            ["scam"]              = "Phishing",
            ["privacy"]           = "Safe Browsing",
            ["malware"]           = "Malware",
            ["2fa"]               = "Two-Factor Authentication (2FA)",
            ["wifi"]              = "Public Wi-Fi",
            ["social engineering"] = "Social Engineering",
            ["updates"]           = "Software Updates",
            ["safe browsing"]     = "Safe Browsing",
            ["backup"]            = "Malware",
            ["encryption"]        = "Safe Browsing"
        };

        public readonly Func<string, string?> FindTopicKey;
        public readonly Func<string, string?> FindTopicName;
        public readonly Predicate<string> HasCyberKeyword;

        private Action<string, string>? _onMatch;

        public ThreatRecognizer()
        {
            FindTopicKey = input =>
            {
                string lower = input.ToLowerInvariant();
                foreach (var (key, kws) in _map)
                {
                    if (kws.Any(kw => lower.Contains(kw)))
                    {
                        string matched = kws.First(kw => lower.Contains(kw));
                        _onMatch?.Invoke(matched, key);
                        return key;
                    }
                }
                return null;
            };

            FindTopicName = input =>
            {
                string? key = FindTopicKey(input);
                return key != null && _toName.TryGetValue(key, out var n) ? n : null;
            };

            HasCyberKeyword = input => FindTopicKey(input) != null;
        }

        public void SetOnMatch(Action<string, string> handler) => _onMatch = handler;

        private static readonly HashSet<string> FollowUpPhrases =
            new(StringComparer.OrdinalIgnoreCase)
        {
            "more", "tell me more", "give me more", "explain more",
            "another tip", "give me another tip", "continue", "go on",
            "more info", "more details", "what else", "and what else"
        };

        public bool IsFollowUp(string input) =>
            FollowUpPhrases.Any(p => input.ToLowerInvariant().Contains(p));

        private static readonly string[] InterestPhrases =
        {
            "interested in", "i like", "i care about", "i'm worried about",
            "i want to know about", "tell me about", "my concern is",
            "i need help with", "teach me about"
        };

        /// <summary>Returns the topic key when the user declares interest, else null.</summary>
        public string? ExtractDeclaredInterest(string input)
        {
            string lower = input.ToLowerInvariant();
            foreach (string phrase in InterestPhrases)
            {
                int idx = lower.IndexOf(phrase, StringComparison.Ordinal);
                if (idx >= 0)
                {
                    string tail = lower[(idx + phrase.Length)..];
                    foreach (var (key, kws) in _map)
                        if (kws.Any(kw => tail.Contains(kw)))
                            return key;
                }
            }
            return null;
        }
    }
}
