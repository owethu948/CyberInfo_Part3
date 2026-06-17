using System;
using System.Collections.Generic;
using System.Linq;

namespace CyberInfo.Engine
{
    /// <summary>
    /// Manages the cybersecurity quiz mini-game.
    /// Provides shuffled questions, answer validation, score tracking, and result grading.
    /// Satisfies Task 2: 12+ questions, multiple-choice and True/False, score feedback.
    /// </summary>
    public class QuizEngine
    {
        // ── Inner type ─────────────────────────────────────────────────────────
        public class Question
        {
            public string       Text         { get; set; } = string.Empty;
            public List<string> Options      { get; set; } = new();
            public int          CorrectIndex { get; set; }
            public string       Explanation  { get; set; } = string.Empty;
            public string       Category     { get; set; } = string.Empty;
        }

        // ── State ──────────────────────────────────────────────────────────────
        private readonly List<Question> _allQuestions;
        private readonly Random         _rand = new();
        private Queue<Question>         _remainingQuestions = new();
        private Question?               _currentQuestion;
        private int                     _score;
        private int                     _questionNumber;  // 1-based, set by GetNextQuestion

        // ── Public read-only state ─────────────────────────────────────────────
        public bool   IsActive             { get; private set; }
        public int    TotalQuestions       => _allQuestions.Count;
        public int    CurrentQuestionNumber => _questionNumber;
        public int    CurrentScore         => _score;

        /// <summary>
        /// The plain-text explanation for the question most recently answered.
        /// Set by SubmitAnswer so the GUI can display it without string-parsing.
        /// </summary>
        public string LastExplanation { get; private set; } = string.Empty;

        // ── Constructor ───────────────────────────────────────────────────────
        public QuizEngine() => _allQuestions = BuildQuestions();

        // ── Question bank (12 questions: 10 multiple-choice, 2 true/false) ────
        private static List<Question> BuildQuestions() => new()
        {
            // ── Multiple choice ────────────────────────────────────────────────
            new Question
            {
                Text         = "What is the most common method attackers use to steal login credentials?",
                Options      = new() { "Brute-force attacks", "Phishing emails", "Keyloggers", "Shoulder surfing" },
                CorrectIndex = 1,
                Explanation  = "Phishing emails trick users into entering credentials on fake websites — by far the most common vector.",
                Category     = "Phishing"
            },
            new Question
            {
                Text         = "Which of the following is the STRONGEST password?",
                Options      = new() { "12345678", "Password123", "T#9kR!mZ@vQ2xLpW", "qwerty" },
                CorrectIndex = 2,
                Explanation  = "Long, random passwords mixing uppercase, lowercase, numbers, and symbols are hardest to crack. Use a password manager to generate them.",
                Category     = "Passwords"
            },
            new Question
            {
                Text         = "What does 2FA stand for?",
                Options      = new() { "Two-Factor Authentication", "Second Factor Access", "Dual Form Approval", "Two-File Authorization" },
                CorrectIndex = 0,
                Explanation  = "Two-Factor Authentication adds a second verification step (e.g. an OTP code) beyond your password, blocking 99.9% of automated attacks.",
                Category     = "2FA"
            },
            new Question
            {
                Text         = "You receive an unexpected email from 'your bank' with a login link. What should you do?",
                Options      = new() { "Click the link to check your account", "Reply asking if it is real", "Call the bank using the number on your card", "Forward it to friends for advice" },
                CorrectIndex = 2,
                Explanation  = "Always verify via the official number on the back of your card — never use contact details from a suspicious email.",
                Category     = "Phishing"
            },
            new Question
            {
                Text         = "Which type of malware encrypts your files and demands payment to unlock them?",
                Options      = new() { "Spyware", "Trojan", "Ransomware", "Adware" },
                CorrectIndex = 2,
                Explanation  = "Ransomware encrypts your data and demands a ransom — often in cryptocurrency. Regular backups are your best defence.",
                Category     = "Malware"
            },
            new Question
            {
                Text         = "What is social engineering in cybersecurity?",
                Options      = new() { "Hacking into social media accounts", "Psychological manipulation to trick people into revealing information", "Writing malicious software code", "Scanning a network for open ports" },
                CorrectIndex = 1,
                Explanation  = "Social engineering exploits human psychology rather than technical vulnerabilities — it is responsible for over 70% of data breaches.",
                Category     = "Social Engineering"
            },
            new Question
            {
                Text         = "Why should you install software updates promptly?",
                Options      = new() { "To receive new features only", "To fix security vulnerabilities before attackers exploit them", "To improve performance only", "All of the above — but patching security holes is the critical reason" },
                CorrectIndex = 3,
                Explanation  = "Updates do all three, but the security reason is critical — attackers exploit unpatched software within hours of a patch's release.",
                Category     = "Updates"
            },
            new Question
            {
                Text         = "What is the safest way to store your passwords?",
                Options      = new() { "In a text file on your desktop", "Written on a sticky note under the keyboard", "In a reputable password manager (e.g. Bitwarden)", "Use the same strong password everywhere" },
                CorrectIndex = 2,
                Explanation  = "Password managers generate unique, strong passwords for every site and store them securely — the only safe approach at scale.",
                Category     = "Passwords"
            },
            new Question
            {
                Text         = "What does 'HTTPS' in a website URL indicate?",
                Options      = new() { "The site is guaranteed free from malware", "The connection between your browser and the server is encrypted", "The site is an official government website", "No user data is collected on the site" },
                CorrectIndex = 1,
                Explanation  = "HTTPS encrypts data in transit. It does NOT guarantee the site itself is safe or trustworthy.",
                Category     = "Safe Browsing"
            },
            new Question
            {
                Text         = "You find a USB stick in your company's car park. What should you do?",
                Options      = new() { "Plug it in to see who it belongs to", "Hand it to IT or security without plugging it in", "Format it first, then check the files", "Take it home and scan it with antivirus" },
                CorrectIndex = 1,
                Explanation  = "Never insert unknown USB drives — attackers drop infected drives on purpose ('USB drop' attack). Only IT personnel should handle them safely.",
                Category     = "Malware"
            },
            // ── True / False ───────────────────────────────────────────────────
            new Question
            {
                Text         = "TRUE or FALSE: Using public Wi-Fi is safe for online banking if your browser shows a padlock icon (HTTPS).",
                Options      = new() { "True", "False" },
                CorrectIndex = 1,
                Explanation  = "FALSE. HTTPS protects data in transit, but the public Wi-Fi network itself could be malicious — an attacker may run an 'evil twin' hotspot. Use mobile data or a VPN for banking.",
                Category     = "Wi-Fi"
            },
            new Question
            {
                Text         = "TRUE or FALSE: Enabling Two-Factor Authentication (2FA) on your email account is especially important because email is used to reset other account passwords.",
                Options      = new() { "True", "False" },
                CorrectIndex = 0,
                Explanation  = "TRUE. Your email is the master key — anyone who controls it can reset nearly every other account password. Protect it first.",
                Category     = "2FA"
            }
        };

        // ── Public methods ─────────────────────────────────────────────────────

        /// <summary>Starts a new quiz session with shuffled questions and resets score.</summary>
        public void Start()
        {
            var shuffled = _allQuestions.OrderBy(_ => _rand.Next()).ToList();
            _remainingQuestions = new Queue<Question>(shuffled);
            _score              = 0;
            _questionNumber     = 0;
            IsActive            = true;
            _currentQuestion    = null;
            LastExplanation     = string.Empty;
        }

        /// <summary>
        /// Dequeues and returns the next question, incrementing the question counter.
        /// Returns null when all questions have been served.
        /// </summary>
        public Question? GetNextQuestion()
        {
            if (!IsActive || _remainingQuestions.Count == 0)
            {
                IsActive = false;
                return null;
            }
            _currentQuestion = _remainingQuestions.Dequeue();
            _questionNumber++;
            return _currentQuestion;
        }

        /// <summary>
        /// Validates the selected answer index (0-based).
        /// Returns whether it was correct, a human-readable result label, and the running score.
        /// The full explanation is also stored in <see cref="LastExplanation"/>.
        /// </summary>
        public (bool Correct, string ResultLabel, int NewScore) SubmitAnswer(int selectedIndex)
        {
            if (_currentQuestion == null)
                throw new InvalidOperationException("No active question. Call GetNextQuestion first.");

            bool correct = selectedIndex == _currentQuestion.CorrectIndex;
            if (correct) _score++;

            // Store explanation cleanly — no emoji prefix, used by GUI directly
            LastExplanation = correct
                ? _currentQuestion.Explanation
                : $"The correct answer was: {(char)('A' + _currentQuestion.CorrectIndex)}) " +
                  $"{_currentQuestion.Options[_currentQuestion.CorrectIndex]}.\n{_currentQuestion.Explanation}";

            string label = correct ? "✅  Correct!" : "❌  Incorrect!";

            _currentQuestion = null;
            if (_remainingQuestions.Count == 0)
                IsActive = false;

            return (correct, label, _score);
        }

        /// <summary>Returns the final score tuple and a motivational message.</summary>
        public (int Score, int Total, string Message) GetFinalResult()
        {
            int pct = TotalQuestions > 0 ? _score * 100 / TotalQuestions : 0;
            string msg = pct switch
            {
                >= 92 => "🏆 Outstanding! You're a certified cybersecurity expert!",
                >= 75 => "🎉 Great job! You have solid, practical knowledge.",
                >= 58 => "👍 Good effort! A few more study sessions and you'll be a pro.",
                >= 40 => "📚 Keep learning — cybersecurity awareness protects you every day.",
                _     => "🛡️ Don't give up! Every attempt teaches you something new. Try again!"
            };
            return (_score, TotalQuestions, msg);
        }

        /// <summary>Resets the quiz to the initial (not-started) state.</summary>
        public void Reset()
        {
            IsActive            = false;
            _remainingQuestions = new Queue<Question>();
            _score              = 0;
            _questionNumber     = 0;
            _currentQuestion    = null;
            LastExplanation     = string.Empty;
        }
    }
}
