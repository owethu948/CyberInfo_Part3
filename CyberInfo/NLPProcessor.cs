using CyberInfo.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CyberInfo.Engine
{
    /// <summary>
    /// Central orchestrator that routes user input through NLP intent detection,
    /// keyword/topic matching, sentiment analysis, scenario challenges, quiz management,
    /// task CRUD, and activity logging.
    ///
    /// All four Part-3 tasks flow through <see cref="ProcessInput"/>.
    /// </summary>
    public class BotEngine
    {
        // ── Sub-engines ────────────────────────────────────────────────────────
        public List<SecurityTopic> Topics      { get; }
        public UserProfile         Memory      { get; }
        public ThreatRecognizer    Keywords    { get; }
        public EmotionAnalyzer     Sentiment   { get; }
        public SecurityTipsLibrary Library     { get; }
        public QuizEngine          Quiz        { get; }
        public NLPProcessor        NLP         { get; }
        public ActivityLogger      ActivityLog { get; }
        public DatabaseManager?    DB          { get; }

        // ── Internal state ────────────────────────────────────────────────────
        private readonly List<SecurityChallenge> _scenarios;
        private SecurityChallenge?               _activeScenario;
        private readonly List<string>            _facts;
        private readonly Random                  _rand = new();

        // ─────────────────────────────────────────────────────────────────────
        //  CONSTRUCTOR
        // ─────────────────────────────────────────────────────────────────────
        public BotEngine()
        {
            Topics      = BuildTopics();
            Memory      = new UserProfile();
            Keywords    = new ThreatRecognizer();
            Sentiment   = new EmotionAnalyzer();
            Library     = new SecurityTipsLibrary();
            Quiz        = new QuizEngine();
            NLP         = new NLPProcessor();
            ActivityLog = new ActivityLogger();
            _scenarios  = BuildScenarios();
            _facts      = BuildFacts();

            Library.OnTipSelected += (topic, _) =>
                System.Diagnostics.Debug.WriteLine($"[Library] tip served for '{topic}'");

            // Try database — graceful fallback to DB-less mode
            try
            {
                DB = new DatabaseManager();
                ActivityLog.Log("Bot initialised — database connected.");
            }
            catch (Exception ex)
            {
                DB = null;
                ActivityLog.Log($"Database unavailable: {ex.Message}  (DB-less mode active)");
                System.Diagnostics.Debug.WriteLine($"[DB] {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  MAIN PROCESSING PIPELINE
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Processes raw user input and returns a bot reply string.
        /// Also outputs the detected sentiment for the GUI status badge.
        /// </summary>
        public string ProcessInput(string input,
                                   out string?                    sentimentResponse,
                                   out EmotionAnalyzer.Sentiment  detectedSentiment)
        {
            input = input.Trim();
            Memory.PushInput(input);

            detectedSentiment = Sentiment.Detect(input);
            sentimentResponse = Sentiment.GetEmpathyResponse(detectedSentiment);
            string empathy    = string.IsNullOrEmpty(sentimentResponse) ? "" : sentimentResponse + "\n\n";

            // ── Priority 1: Active chat-mode quiz ─────────────────────────────
            if (Quiz.IsActive)
            {
                // Accept A/B/C/D or 1/2/3/4 as answers
                int answerIdx = ParseQuizAnswer(input);
                if (answerIdx >= 0)
                {
                    var (correct, label, score) = Quiz.SubmitAnswer(answerIdx);
                    ActivityLog.Log($"Chat-quiz Q{Quiz.CurrentQuestionNumber}: {(correct ? "Correct" : "Wrong")}");

                    var next = Quiz.GetNextQuestion();
                    if (next == null)
                    {
                        // Quiz complete
                        var (finalScore, total, msg) = Quiz.GetFinalResult();
                        ActivityLog.Log($"Chat-quiz complete. Score: {finalScore}/{total}");
                        return $"{label}\n{Quiz.LastExplanation}\n\n" +
                               $"🏆 QUIZ COMPLETE!  {finalScore} / {total}\n{msg}\n\n" +
                               "Switch to the 🎮 Quiz tab to play again anytime!";
                    }
                    return $"{label}\n{Quiz.LastExplanation}\n\n" +
                           $"❓ QUESTION {Quiz.CurrentQuestionNumber}/{Quiz.TotalQuestions}\n{next.Text}\n\n" +
                           string.Join("\n", next.Options.Select((o, i) => $"   {(char)('A' + i)}) {o}")) +
                           "\n\nType A, B, C or D (or 1, 2, 3, 4) to answer.";
                }
                else
                {
                    return "Please answer with A, B, C or D  (or the matching number).";
                }
            }

            // ── Priority 2: Active scenario challenge ─────────────────────────
            if (_activeScenario != null)
            {
                int idx = ParseQuizAnswer(input);
                if (idx >= 0 && idx < _activeScenario.Options.Count)
                {
                    bool correct        = idx == _activeScenario.CorrectOptionIndex;
                    string feedback     = _activeScenario.Feedback;
                    _activeScenario     = null;
                    ActivityLog.Log($"Scenario answered (option {idx + 1}): {(correct ? "Correct" : "Wrong")}.");
                    return (correct ? "✅ " : "❌ ") + feedback +
                           "\n\nWould you like another scenario? Type 'scenario' to try again.";
                }
            }

            // ── Priority 3: NLP intent detection ─────────────────────────────
            var (intent, numericId, extractedText, reminderDays) = NLP.ParseInput(input);

            switch (intent)
            {
                case Intent.Greeting:
                    string nameGreet = Memory.NameCaptured ? $", {Memory.UserName}" : "";
                    ActivityLog.Log("Greeting detected.");
                    return $"Hello{nameGreet}! 👋  How can I help you today?\n\n{BuildTopicMenu()}";

                case Intent.Farewell:
                    ActivityLog.Log("User said goodbye.");
                    return Memory.NameCaptured
                        ? $"Goodbye, {Memory.UserName}! Stay safe and vigilant online. 🛡️"
                        : "Goodbye! Stay safe online. 🛡️";

                case Intent.AskHelp:
                    ActivityLog.Log("Help requested.");
                    return BuildHelpText();

                case Intent.AskName:
                    return Memory.NameCaptured
                        ? $"Your name is {Memory.UserName}. 😊"
                        : "I don't know your name yet! What should I call you?";

                case Intent.AddTask:
                    return empathy + HandleAddTask(extractedText);

                case Intent.ViewTasks:
                    return empathy + HandleViewTasks();

                case Intent.DeleteTask:
                    return HandleDeleteTask(numericId);

                case Intent.CompleteTask:
                    return HandleCompleteTask(numericId);

                case Intent.SetReminder:
                    return empathy + HandleSetReminder(extractedText, reminderDays);

                case Intent.StartQuiz:
                    return empathy + HandleStartQuizInChat();

                case Intent.ShowActivityLog:
                    return HandleShowActivityLog();
            }

            // ── Priority 4: Keyword / topic routing ───────────────────────────
            string lower = input.ToLowerInvariant();

            if (lower.Contains("scenario") || lower.Contains("challenge"))
                return HandleScenario();

            if (lower.Contains("fact") || lower.Contains("did you know"))
                return HandleFact();

            if (lower.Contains(" tip") || lower == "tip" || lower.Contains("give me a tip"))
                return HandleTip();

            if (Keywords.IsFollowUp(input) && !string.IsNullOrEmpty(Memory.LastTopic))
                return HandleFollowUp();

            string? declaredKey = Keywords.ExtractDeclaredInterest(input);
            if (declaredKey != null)
                return HandleDeclaredInterest(declaredKey, empathy);

            string? matchedKey = Keywords.FindTopicKey(input);
            if (matchedKey != null)
                return HandleTopicByKey(matchedKey, empathy);

            // Number select (1-8)
            if (int.TryParse(input, out int topicNum) && topicNum >= 1 && topicNum <= Topics.Count)
                return HandleTopicByNumber(topicNum, empathy);

            // Direct topic name match
            var directTopic = Topics.Find(
                t => t.Name.Equals(input, StringComparison.OrdinalIgnoreCase));
            if (directTopic != null)
                return HandleDirectTopic(directTopic, empathy);

            // ── Priority 5: First-message name capture ────────────────────────
            if (!Memory.NameCaptured && input.Length >= 2 && !input.Contains(' ') || 
                !Memory.NameCaptured && input.Split(' ').Length <= 3 && input.Length <= 30)
            {
                Memory.UserName = input;
                ActivityLog.Log($"User name captured: '{input}'.");
                return $"Nice to meet you, {input}! 🎉\n\n{BuildTopicMenu()}";
            }

            // ── Fallback ──────────────────────────────────────────────────────
            return empathy + BuildFallbackResponse();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  SCENARIO ACCESSORS
        // ─────────────────────────────────────────────────────────────────────
        public bool HasActiveScenario()              => _activeScenario != null;
        public SecurityChallenge? GetActiveScenario() => _activeScenario;
        public void CancelScenario()                  => _activeScenario = null;

        // ─────────────────────────────────────────────────────────────────────
        //  INTENT HANDLERS
        // ─────────────────────────────────────────────────────────────────────

        private string HandleAddTask(string? text)
        {
            if (DB == null)
                return "⚠️ Database is not available. Ensure MySQL is running.\n" +
                       "Switch to the 📋 Tasks tab to manage tasks (DB required).";

            string title = string.IsNullOrWhiteSpace(text) ? "cybersecurity task" : text;
            try
            {
                int id = DB.AddTask(title, "Added via chatbot");
                ActivityLog.Log($"Task added: '{title}' (ID {id}).");
                return $"✅ Task added: '{title}'  (ID {id})\n\n" +
                       "Type 'view tasks' to see all pending tasks, or switch to the 📋 Tasks tab.";
            }
            catch (Exception ex)
            {
                ActivityLog.Log($"Task add failed: {ex.Message}");
                return $"⚠️ Could not save task: {ex.Message}";
            }
        }

        private string HandleViewTasks()
        {
            if (DB == null)
                return "⚠️ Database unavailable. Switch to the 📋 Tasks tab.";
            try
            {
                var tasks = DB.GetTasks(false);
                if (!tasks.Any())
                    return "📭 No pending cybersecurity tasks.\n\nAdd one with: 'add task to enable 2FA'";

                string list = string.Join("\n", tasks.Select(t =>
                    $"   {t.Id}. {t.Title}" +
                    (t.Reminder.HasValue ? $"  ⏰ {t.Reminder:yyyy-MM-dd}" : "")
                ));
                ActivityLog.Log("Tasks viewed via chat.");
                return $"📝 PENDING CYBERSECURITY TASKS:\n{list}\n\n" +
                       "• Mark done:  'mark task X complete'\n" +
                       "• Delete:     'delete task X'\n" +
                       "• Full UI:    switch to the 📋 Tasks tab";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        private string HandleDeleteTask(int? id)
        {
            if (DB == null) return "⚠️ Database unavailable.";
            if (!id.HasValue) return "Please specify the task number — e.g. 'delete task 3'.";
            try
            {
                bool ok = DB.DeleteTask(id.Value);
                ActivityLog.Log($"Task {id} deleted: {(ok ? "success" : "not found")}.");
                return ok ? $"🗑️ Task {id} deleted." : $"Task {id} not found.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        private string HandleCompleteTask(int? id)
        {
            if (DB == null) return "⚠️ Database unavailable.";
            if (!id.HasValue) return "Please specify the task number — e.g. 'mark task 1 complete'.";
            try
            {
                bool ok = DB.MarkTaskCompleted(id.Value);
                ActivityLog.Log($"Task {id} completed: {(ok ? "yes" : "not found")}.");
                return ok
                    ? $"✅ Task {id} marked as completed! Great progress on your cybersecurity goals. 🎉"
                    : $"Task {id} was not found.";
            }
            catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
        }

        private string HandleSetReminder(string? text, int? days)
        {
            if (DB == null) return "⚠️ Database unavailable.";
            if (!days.HasValue) return "When should I remind you? e.g. 'remind me in 3 days'.";

            DateTime date = DateTime.Today.AddDays(days.Value);
            string   task = "Reminder: " + (string.IsNullOrWhiteSpace(text) ? "check cybersecurity tasks" : text);
            try
            {
                int id = DB.AddTask(task, "Auto-reminder", date);
                ActivityLog.Log($"Reminder set: '{task}' for {date:yyyy-MM-dd} (task ID {id}).");
                return $"⏰ Reminder set!\n\n'{task}'\nDate: {date:dd MMM yyyy}";
            }
            catch (Exception ex) { return $"⚠️ Could not set reminder: {ex.Message}"; }
        }

        private string HandleStartQuizInChat()
        {
            Quiz.Start();
            ActivityLog.Log("Chat-quiz started.");
            var q = Quiz.GetNextQuestion();
            if (q == null) return "Quiz could not start — please try again.";

            return $"🎮 CYBERSECURITY QUIZ  ({Quiz.TotalQuestions} questions)\n\n" +
                   $"❓ QUESTION 1/{Quiz.TotalQuestions}\n{q.Text}\n\n" +
                   string.Join("\n", q.Options.Select((o, i) => $"   {(char)('A' + i)}) {o}")) +
                   "\n\nType A, B, C or D (or 1, 2, 3, 4) to answer." +
                   "\n\n💡 Tip: switch to the 🎮 Quiz tab for the full interactive experience!";
        }

        private string HandleShowActivityLog()
        {
            var logs = ActivityLog.GetRecentLogs(10);
            string text = logs.Any()
                ? string.Join("\n", logs)
                : "No recent activities recorded yet.";
            ActivityLog.Log("Activity log viewed via chat.");
            return $"📋 RECENT ACTIVITY LOG (last 10):\n\n{text}\n\n" +
                   "Switch to the 📜 Activity Log tab to export or page through the full history.";
        }

        private string HandleScenario()
        {
            _activeScenario = _scenarios[_rand.Next(_scenarios.Count)];
            string opts = string.Join("\n",
                _activeScenario.Options.Select((o, i) => $"   {(char)('A' + i)}) {o}"));
            ActivityLog.Log("Scenario challenge started.");
            return $"📋 SCENARIO CHALLENGE\n\n{_activeScenario.Description}\n\n{opts}\n\n" +
                   "Type A, B, C or D (or 1, 2, 3, 4) to answer.";
        }

        private string HandleFact()
        {
            string fact = _facts[_rand.Next(_facts.Count)];
            ActivityLog.Log("Cyber fact shared.");
            return fact;
        }

        private string HandleTip()
        {
            string key = MapTopicNameToKey(Memory.LastTopic ?? "password");
            string tip = Library.GetRandomTip(key);
            ActivityLog.Log($"Security tip shared for '{key}'.");
            return $"💡 SECURITY TIP\n\n{tip}";
        }

        private string HandleFollowUp()
        {
            string tip = Library.GetRandomTip(MapTopicNameToKey(Memory.LastTopic!));
            ActivityLog.Log($"Follow-up tip on '{Memory.LastTopic}'.");
            return $"Here's another tip about {Memory.LastTopic}:\n\n{tip}";
        }

        private string HandleDeclaredInterest(string key, string empathy)
        {
            string name = MapKeyToTopicName(key);
            Memory.RememberInterest(name);
            ActivityLog.Log($"User expressed interest in {name}.");
            return $"{empathy}Got it — you're interested in {name}. Here's what you need to know:\n\n{GetTopicResponse(name)}";
        }

        private string HandleTopicByKey(string key, string empathy)
        {
            string name   = MapKeyToTopicName(key);
            Memory.LastTopic = name;
            Memory.RememberInterest(name);
            ActivityLog.Log($"Topic discussed: {name}.");
            return empathy + GetTopicResponse(name);
        }

        private string HandleTopicByNumber(int num, string empathy)
        {
            var topic = Topics[num - 1];
            Memory.LastTopic = topic.Name;
            Memory.RememberInterest(topic.Name);
            ActivityLog.Log($"Topic selected by number: {topic.Name}.");
            return empathy + topic.BuildGuiResponse();
        }

        private string HandleDirectTopic(SecurityTopic topic, string empathy)
        {
            Memory.LastTopic = topic.Name;
            Memory.RememberInterest(topic.Name);
            ActivityLog.Log($"Topic selected by name: {topic.Name}.");
            return empathy + topic.BuildGuiResponse();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  HELPER METHODS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Parses quiz / scenario answer input.
        /// Accepts A/B/C/D or 1/2/3/4 (case-insensitive).
        /// Returns a 0-based index or -1 if not recognised.
        /// </summary>
        private static int ParseQuizAnswer(string input)
        {
            string s = input.Trim().ToUpperInvariant();
            if (s.Length == 1)
            {
                if (s[0] >= 'A' && s[0] <= 'D') return s[0] - 'A';
                if (s[0] >= '1' && s[0] <= '4') return s[0] - '1';
            }
            return -1;
        }

        private string GetTopicResponse(string name)
        {
            var t = Topics.Find(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            return t?.BuildGuiResponse() ?? $"I have info on {name}. Try typing the topic name again!";
        }

        private string BuildTopicMenu() =>
            "I can teach you about:\n\n" +
            "   1. Passwords              5. Malware\n" +
            "   2. Phishing               6. Social Engineering\n" +
            "   3. Safe Browsing          7. Software Updates\n" +
            "   4. Two-Factor Auth (2FA)  8. Public Wi-Fi\n\n" +
            "Type a number, a topic name, or:\n" +
            "   • 'scenario' — test yourself on a real-world situation\n" +
            "   • 'fact'     — get a cybersecurity statistic\n" +
            "   • 'tip'      — get a security tip\n" +
            "   • 'start quiz' — launch the full quiz game\n" +
            "   • 'add task to …' / 'view tasks'\n" +
            "   • 'show activity log'\n" +
            "   • 'help' — full command list";

        private string BuildHelpText() =>
            "🛡️  WHAT I CAN DO\n\n" +
            "📚  LEARN cybersecurity:\n" +
            "   Type a topic: Phishing, Passwords, Malware, 2FA,\n" +
            "   Safe Browsing, Social Engineering, Software Updates, Public Wi-Fi\n" +
            "   Or type a number 1–8, or ask for 'scenario', 'fact', 'tip'\n\n" +
            "📋  MANAGE TASKS (or use the Tasks tab):\n" +
            "   'add task to enable 2FA'\n" +
            "   'view tasks'\n" +
            "   'mark task 1 complete'\n" +
            "   'delete task 2'\n" +
            "   'remind me in 3 days to update my passwords'\n\n" +
            "🎮  QUIZ:  'start quiz'\n" +
            "📜  LOG:   'show activity log'\n" +
            "👋  CHAT:  greet me, ask questions, or express how you feel!";

        private string BuildFallbackResponse() =>
            "I can help with cybersecurity. Try:\n" +
            "   • A topic name: phishing, malware, passwords, 2fa, wifi…\n" +
            "   • Type a number 1–8\n" +
            "   • 'scenario', 'fact', or 'tip'\n" +
            "   • 'add task to …', 'view tasks', 'start quiz'\n" +
            "   • 'show activity log'\n" +
            "   • 'help' for the full command list";

        // ── Key / name mapping ─────────────────────────────────────────────────
        private static string MapKeyToTopicName(string key) => key.ToLowerInvariant() switch
        {
            "phishing"           => "Phishing",
            "password"           => "Passwords",
            "malware"            => "Malware",
            "2fa"                => "Two-Factor Authentication (2FA)",
            "wifi"               => "Public Wi-Fi",
            "social engineering" => "Social Engineering",
            "updates"            => "Software Updates",
            "safe browsing"      => "Safe Browsing",
            "scam"               => "Phishing",
            "privacy"            => "Safe Browsing",
            "backup"             => "Malware",
            "encryption"         => "Safe Browsing",
            _                    => "Passwords"
        };

        private static string MapTopicNameToKey(string name) => name.ToLowerInvariant() switch
        {
            "passwords"                       => "password",
            "phishing"                        => "phishing",
            "safe browsing"                   => "safe browsing",
            "two-factor authentication (2fa)" => "2fa",
            "malware"                         => "malware",
            "social engineering"              => "social engineering",
            "software updates"                => "updates",
            "public wi-fi"                    => "wifi",
            _                                 => "password"
        };

        // ─────────────────────────────────────────────────────────────────────
        //  DATA BUILDERS
        // ─────────────────────────────────────────────────────────────────────
        private static List<SecurityTopic> BuildTopics() => new()
        {
            new SecurityTopic("Passwords",
                "A password is a secret word or phrase used to authenticate your identity when logging into systems.",
                "Weak or reused passwords are the #1 way attackers compromise accounts.",
                "Whenever you log into email, banking, social media, streaming, or work systems.",
                "Use a password manager, enable 2FA, never reuse passwords, use 16+ random characters.",
                "NIST SP 800-63B Digital Identity Guidelines"),

            new SecurityTopic("Phishing",
                "Fraudulent attempts to obtain sensitive information by impersonating a trustworthy entity via email, SMS, or fake websites.",
                "Phishing causes billions in annual losses and is the top initial infection vector globally.",
                "Via email, SMS, WhatsApp, phone calls, or fake websites mimicking banks, SARS, or delivery services.",
                "Check sender addresses carefully, hover over links before clicking, never share OTPs, report suspicious emails.",
                "SABRIC Annual Crime Stats 2024"),

            new SecurityTopic("Safe Browsing",
                "Practices that protect you from malicious websites, harmful downloads, and online tracking.",
                "One wrong click can silently install malware or hand over your credentials to a criminal.",
                "When visiting unfamiliar sites, clicking ads, downloading software, or entering personal/payment info.",
                "Look for HTTPS + padlock, use an ad-blocker, avoid piracy sites, keep your browser updated.",
                "Google Safe Browsing Transparency Report"),

            new SecurityTopic("Two-Factor Authentication (2FA)",
                "An extra verification step beyond your password — usually a one-time code from an app or SMS.",
                "2FA blocks 99.9% of automated credential-stuffing and phishing attacks even when passwords are stolen.",
                "On every account that supports it: email, banking, social media, cloud storage, work systems.",
                "Use an authenticator app (not SMS where avoidable), store backup codes offline, consider a hardware key.",
                "Microsoft Digital Defense Report 2024"),

            new SecurityTopic("Malware",
                "Malicious software designed to damage, disrupt, or gain unauthorised access — includes viruses, ransomware, trojans, spyware.",
                "Ransomware attacks in South Africa increased 57% in 2024, costing businesses millions per incident.",
                "When opening email attachments, downloading cracked software, connecting unknown USB drives, or clicking pop-ups.",
                "Keep antivirus active, install updates immediately, back up data regularly, never plug in unknown USBs.",
                "Kaspersky Security Bulletin 2024"),

            new SecurityTopic("Social Engineering",
                "Psychological manipulation techniques that trick people into revealing confidential information or performing unsafe actions.",
                "Over 70% of data breaches involve a human element — social engineering exploits trust, not technology.",
                "Via phone (vishing), in-person impersonation, fake IT support calls, email (phishing), and pretexting scenarios.",
                "Always verify caller identity through official channels, never share passwords or OTPs, slow down when pressured.",
                "Verizon Data Breach Investigations Report 2024"),

            new SecurityTopic("Software Updates",
                "Patches released by software vendors to fix security vulnerabilities, bugs, and improve functionality.",
                "Attackers exploit unpatched vulnerabilities within hours of public disclosure — updates close those windows.",
                "Applies to Windows/macOS, mobile OS, browsers, apps, router firmware, and IoT smart devices.",
                "Enable automatic updates, restart devices after patching, never indefinitely postpone security updates.",
                "CISA Known Exploited Vulnerabilities Catalog"),

            new SecurityTopic("Public Wi-Fi",
                "Open wireless networks in cafes, airports, hotels, and shopping centres that any device can join.",
                "Attackers on the same network can intercept unencrypted traffic or create convincing fake hotspots.",
                "Whenever you connect at airports, hotels, restaurants, libraries, or any venue's free Wi-Fi.",
                "Use a VPN, avoid sensitive logins on public Wi-Fi, verify the network name with staff, prefer mobile data.",
                "OWASP Wireless Security Testing Guide")
        };

        private static List<SecurityChallenge> BuildScenarios() => new()
        {
            new SecurityChallenge(
                "You receive an email from 'SASSA' saying your grant payment is on hold. It contains a link to 'verify your identity' and asks for your ID number and password. What do you do?",
                new() { "Click the link and fill in your details", "Reply to the email asking for more information", "Forward it to SASSA's official fraud line and delete it", "Call the number listed in the email immediately" },
                2, "Correct! You recognised a phishing attempt. Never click links or call numbers from unsolicited messages — always use official channels found on the organisation's real website."),

            new SecurityChallenge(
                "You are at OR Tambo airport and need to check your bank balance urgently. A free Wi-Fi called 'Airport_Free_WiFi' appears. What is the safest action?",
                new() { "Connect and check quickly — it'll be fine", "Use a VPN first, then open your banking app", "Use your phone's mobile data hotspot instead", "Ask the nearest shop assistant for the correct Wi-Fi name" },
                2, "Best choice! Mobile data is encrypted end-to-end and sidesteps the risk of malicious hotspots entirely."),

            new SecurityChallenge(
                "Someone calls claiming to be from your company's IT helpdesk. They say your computer is infected and need remote access immediately. What do you do?",
                new() { "Give them remote access — IT must know best", "Hang up and call the official IT helpdesk number yourself", "Ask them to email you instead", "Let them connect but watch what they do" },
                1, "Perfect! Always verify via the official helpdesk number, never via a contact provided by the caller. Social engineers impersonate IT support constantly."),

            new SecurityChallenge(
                "You spot a USB stick on the floor outside your office. What do you do?",
                new() { "Plug it into your computer to identify the owner", "Hand it to IT security without plugging it in anywhere", "Format it and use it — free storage!", "Take it home and scan it with antivirus" },
                1, "Correct! Attackers deliberately drop infected USB drives hoping curious employees will plug them in. Only IT professionals should handle unknown devices safely."),

            new SecurityChallenge(
                "You receive a WhatsApp message from your 'bank' warning of suspicious activity. The link in the message looks exactly like your bank's real website. What do you do?",
                new() { "Click the link and log in to check", "Ignore it — banks don't use WhatsApp", "Call your bank using the number printed on the back of your card", "Reply STOP to unsubscribe from alerts" },
                2, "Well done! The number on your card or the back of your bank statement is always the safe contact point — never a number or link in a message, however convincing it looks.")
        };

        private static List<string> BuildFacts() => new()
        {
            "📊 In South Africa, over 80% of organisations experienced a phishing attack in 2024.  (SABRIC)",
            "📊 Only 57% of South Africans use unique passwords for every account.  (security.org 2024)",
            "📊 Ransomware attacks cost South African businesses an average of R2.5 million per incident.  (Sophos)",
            "📊 Two-Factor Authentication would have prevented 96% of bulk phishing attacks.  (Microsoft)",
            "📊 The most common password in South Africa is still 'password123'.  (NordPass 2024)",
            "📊 34% of South African adults have been victims of financial fraud.  (SA Fraud Prevention Services)",
            "📊 Over 65% of public Wi-Fi hotspots can be intercepted with basic, freely available tools.  (Kaspersky)",
            "📊 Software updates patch an average of 60–100 security holes every month.  (CISA)",
            "📊 Social engineering is involved in 75% of all data breaches globally.  (Verizon DBIR 2024)",
            "📊 The average time to detect a data breach is 207 days — nearly 7 months.  (IBM Security Cost of a Breach 2024)",
            "📊 South Africa ranks 3rd globally for cybercrime density per internet user.  (Statista 2024)",
            "📊 More than 1 billion unique passwords were leaked online in 2024.  (SpyCloud Annual Credential Exposure Report)",
            "📊 Over 90% of cyber incidents start with a phishing email.  (Proofpoint State of the Phish 2024)"
        };
    }
}
