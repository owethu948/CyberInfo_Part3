using System;
using System.Text.RegularExpressions;

namespace CyberInfo.Engine
{
    /// <summary>
    /// Simulates NLP by parsing user input with regex patterns to detect intents.
    /// Handles natural language variations so users are not limited to exact keywords.
    /// Task 3 requirement.
    /// </summary>
    public enum Intent
    {
        Unknown,
        AddTask,
        ViewTasks,
        DeleteTask,
        CompleteTask,
        StartQuiz,
        ShowActivityLog,
        SetReminder,
        Greeting,
        Farewell,
        AskHelp,
        AskName
    }

    public class NLPProcessor
    {
        // ── Compiled regex patterns ────────────────────────────────────────────
        private static readonly Regex AddTaskRegex = new(
            @"(?i)(add|create|new|set\s+up)\s+(task|reminder|to\s*do)|" +
            @"remind\s+me\s+to|i\s+need\s+to\s+(remember|do)|" +
            @"can\s+you\s+(add|create|remind)",
            RegexOptions.Compiled);

        private static readonly Regex ViewTasksRegex = new(
            @"(?i)(view|show|list|display|what\s+are|see|check)\s+(my\s+)?" +
            @"(tasks|task\s+list|to\s*dos?|reminders?|pending)",
            RegexOptions.Compiled);

        private static readonly Regex DeleteTaskRegex = new(
            @"(?i)(delete|remove|erase|cancel)\s+(task|reminder)\s*#?(\d+)",
            RegexOptions.Compiled);

        private static readonly Regex CompleteTaskRegex = new(
            @"(?i)(mark|complete|finish|done|tick\s+off)\s+(task|reminder)?\s*#?(\d+)|" +
            @"task\s*#?(\d+)\s+(complete|done|finished)",
            RegexOptions.Compiled);

        private static readonly Regex StartQuizRegex = new(
            @"(?i)(start|take|play|begin|launch|run)\s+(a\s+)?(quiz|test|game|challenge)|" +
            @"quiz\s+me|test\s+my\s+knowledge|cybersecurity\s+quiz|" +
            @"i\s+want\s+to\s+(play|take|do)\s+(a\s+)?quiz",
            RegexOptions.Compiled);

        private static readonly Regex ActivityLogRegex = new(
            @"(?i)(show|view|display|list|see|check)\s+(activity\s+log|history|" +
            @"what\s+have\s+you\s+done|recent\s+actions?|log|what\s+happened)",
            RegexOptions.Compiled);

        private static readonly Regex SetReminderRegex = new(
            @"(?i)remind\s+me\s+in\s+(\d+)\s+(day|days|week|weeks)|" +
            @"set\s+a?\s*reminder\s+(for\s+)?(\d+)\s+(day|days|week|weeks)|" +
            @"remind\s+me\s+on\s+(\d{4}-\d{2}-\d{2})",
            RegexOptions.Compiled);

        private static readonly Regex GreetingRegex = new(
            @"(?i)^(hi|hello|hey|howdy|good\s+(morning|afternoon|evening)|" +
            @"sup|what'?s\s+up|yo|greetings)[\s!.,]*$",
            RegexOptions.Compiled);

        private static readonly Regex FarewellRegex = new(
            @"(?i)^(bye|goodbye|cya|see\s+you|later|farewell|" +
            @"take\s+care|quit|exit|done|thank\s+you|thanks)[\s!.,]*$",
            RegexOptions.Compiled);

        private static readonly Regex HelpRegex = new(
            @"(?i)(help|what\s+can\s+you\s+do|what\s+do\s+you\s+know|" +
            @"commands|options|features|how\s+do\s+i\s+use)",
            RegexOptions.Compiled);

        private static readonly Regex NameRegex = new(
            @"(?i)(what('?s|\s+is)\s+(my\s+)?name|do\s+you\s+know\s+(my\s+)?name|" +
            @"who\s+am\s+i|remember\s+my\s+name)",
            RegexOptions.Compiled);

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Parses the user's input and returns the detected intent along with extracted values.
        /// </summary>
        /// <returns>
        /// Tuple of (intent, numericTaskId, extractedText, reminderDays)
        /// </returns>
        public (Intent intent, int? numericId, string? extractedText, int? reminderDays)
            ParseInput(string input)
        {
            input = input.Trim();

            // ── Exact-style intents (checked first to avoid false positives) ──

            var deleteMatch = DeleteTaskRegex.Match(input);
            if (deleteMatch.Success)
                return (Intent.DeleteTask, int.Parse(deleteMatch.Groups[3].Value), null, null);

            var completeMatch = CompleteTaskRegex.Match(input);
            if (completeMatch.Success)
            {
                int id = completeMatch.Groups[3].Success
                    ? int.Parse(completeMatch.Groups[3].Value)
                    : int.Parse(completeMatch.Groups[4].Value);
                return (Intent.CompleteTask, id, null, null);
            }

            var reminderMatch = SetReminderRegex.Match(input);
            if (reminderMatch.Success)
            {
                int days = 0;
                // "remind me in X days/weeks"
                if (reminderMatch.Groups[1].Success)
                {
                    days = int.Parse(reminderMatch.Groups[1].Value);
                    string unit = reminderMatch.Groups[2].Value.ToLowerInvariant();
                    if (unit.StartsWith("week")) days *= 7;
                }
                // "set a reminder for X days"
                else if (reminderMatch.Groups[4].Success)
                {
                    days = int.Parse(reminderMatch.Groups[4].Value);
                    string unit = reminderMatch.Groups[5].Value.ToLowerInvariant();
                    if (unit.StartsWith("week")) days *= 7;
                }
                // Extract the task text after the reminder directive
                string reminderText = ExtractTaskTitle(input);
                return (Intent.SetReminder, null, reminderText, days > 0 ? days : 7);
            }

            if (AddTaskRegex.IsMatch(input))
            {
                string title = ExtractTaskTitle(input);
                return (Intent.AddTask, null, title, null);
            }

            if (ViewTasksRegex.IsMatch(input))
                return (Intent.ViewTasks, null, null, null);

            if (StartQuizRegex.IsMatch(input))
                return (Intent.StartQuiz, null, null, null);

            if (ActivityLogRegex.IsMatch(input))
                return (Intent.ShowActivityLog, null, null, null);

            if (GreetingRegex.IsMatch(input))
                return (Intent.Greeting, null, null, null);

            if (FarewellRegex.IsMatch(input))
                return (Intent.Farewell, null, null, null);

            if (HelpRegex.IsMatch(input))
                return (Intent.AskHelp, null, null, null);

            if (NameRegex.IsMatch(input))
                return (Intent.AskName, null, null, null);

            return (Intent.Unknown, null, null, null);
        }

        /// <summary>
        /// Strips leading directive phrases to isolate the task description.
        /// E.g. "add task to enable 2FA" → "enable 2FA"
        /// </summary>
        private static string ExtractTaskTitle(string input)
        {
            string clean = Regex.Replace(input,
                @"(?i)^.*?(add\s+task|create\s+task|new\s+task|remind\s+me\s+to|" +
                @"i\s+need\s+to\s+(remember|do)|can\s+you\s+(add|create|remind\s+me\s+to))" +
                @"\s*(to\s+)?", "").Trim();
            return string.IsNullOrEmpty(clean) ? "cybersecurity task" : clean;
        }
    }
}
