using CyberInfo.Engine;
using CyberInfo.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace CyberInfo
{
    /// <summary>
    /// Code-behind for the main application window.
    /// Manages four tab panels: Chat, Task Assistant, Quiz Game, and Activity Log.
    ///
    /// All UI interaction funnels through the BotEngine for consistent NLP + logging.
    /// </summary>
    public partial class MainWindow : Window
    {
        // ── Core engine ────────────────────────────────────────────────────────
        private readonly BotEngine _bot;

        // ── Quiz panel state ──────────────────────────────────────────────────
        private int              _quizElapsedSeconds;
        private DispatcherTimer? _quizTimer;
        private bool             _answerSubmitted;   // guard against double-click

        // ── Task panel state ──────────────────────────────────────────────────
        private bool _showCompletedTasks;

        // ── Activity log paging ───────────────────────────────────────────────
        private int _logPageSize = 10;

        // ── Scenario quiz (chat panel) ────────────────────────────────────────
        private SecurityChallenge? _activeScenario;

        // ─────────────────────────────────────────────────────────────────────
        //  CONSTRUCTOR
        // ─────────────────────────────────────────────────────────────────────
        public MainWindow()
        {
            InitializeComponent();
            _bot = new BotEngine();

            _bot.Keywords.SetOnMatch((kw, topic) =>
                System.Diagnostics.Debug.WriteLine($"[Keyword] '{kw}' → {topic}"));

            Loaded += async (_, _) =>
            {
                AudioManager.PlayGreetingAsync();
                await Task.Delay(300);
                AppendBubble("Hi there! I'm CyberInfo — your Cybersecurity Awareness Bot. 🛡️\n\n" +
                             "Before we dive in, what should I call you?", isUser: false);
                InputBox.Focus();
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        //  NAVIGATION
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Hides all panels and shows the requested one.</summary>
        private void ShowPanel(string name)
        {
            PanelChat.Visibility  = Visibility.Collapsed;
            PanelTasks.Visibility = Visibility.Collapsed;
            PanelQuiz.Visibility  = Visibility.Collapsed;
            PanelLog.Visibility   = Visibility.Collapsed;

            switch (name)
            {
                case "Chat":
                    PanelChat.Visibility = Visibility.Visible;
                    TopBarTitle.Text = "CyberInfo  |  Chat";
                    InputBox.Focus();
                    break;

                case "Tasks":
                    PanelTasks.Visibility = Visibility.Visible;
                    TopBarTitle.Text = "CyberInfo  |  Task Assistant";
                    RefreshTaskList();
                    break;

                case "Quiz":
                    PanelQuiz.Visibility = Visibility.Visible;
                    TopBarTitle.Text = "CyberInfo  |  Cybersecurity Quiz";
                    break;

                case "Log":
                    PanelLog.Visibility = Visibility.Visible;
                    TopBarTitle.Text = "CyberInfo  |  Activity Log";
                    RefreshLogPanel();
                    break;
            }
        }

        // Sidebar navigation clicks
        private void Nav_Chat_Click(object s, RoutedEventArgs e)  => ShowPanel("Chat");
        private void Nav_Tasks_Click(object s, RoutedEventArgs e) => ShowPanel("Tasks");
        private void Nav_Quiz_Click(object s, RoutedEventArgs e)  => ShowPanel("Quiz");
        private void Nav_Log_Click(object s, RoutedEventArgs e)   => ShowPanel("Log");

        // Quick-action buttons jump to Chat and inject a command
        private void Sidebar_Scenario_Click(object s, RoutedEventArgs e) { ShowPanel("Chat"); SendUserMessage("scenario"); }
        private void Sidebar_Fact_Click(object s, RoutedEventArgs e)     { ShowPanel("Chat"); SendUserMessage("fact"); }
        private void Sidebar_Tip_Click(object s, RoutedEventArgs e)      { ShowPanel("Chat"); SendUserMessage("tip"); }

        // ─────────────────────────────────────────────────────────────────────
        //  CHAT PANEL
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Appends a styled bubble to the chat panel.
        /// User bubbles are right-aligned / pink; bot bubbles are left-aligned / white.
        /// </summary>
        private void AppendBubble(string text, bool isUser)
        {
            Dispatcher.Invoke(() =>
            {
                var senderLabel = new TextBlock
                {
                    Text       = isUser ? "You" : "CyberInfo",
                    FontSize   = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = (Brush)FindResource("AccentPink"),
                    Margin     = new Thickness(0, 0, 0, 3)
                };

                var body = new TextBlock
                {
                    Text         = text,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize     = 13,
                    Foreground   = (Brush)FindResource("TextPrimary")
                };

                var stack = new StackPanel();
                stack.Children.Add(senderLabel);
                stack.Children.Add(body);

                var bubble = new Border
                {
                    Child               = stack,
                    CornerRadius        = new CornerRadius(12),
                    Margin              = new Thickness(0, 4, 0, 4),
                    Padding             = new Thickness(12, 8, 12, 8),
                    MaxWidth            = 580,
                    Background          = isUser
                        ? (Brush)FindResource("BgUserBubble")
                        : (Brush)FindResource("BgBotBubble"),
                    HorizontalAlignment = isUser
                        ? HorizontalAlignment.Right
                        : HorizontalAlignment.Left
                };

                ChatPanel.Children.Add(bubble);
                ChatScroll.ScrollToEnd();
            });
        }

        /// <summary>Shows the typing indicator, waits, then appends the bot reply.</summary>
        private async Task AddBotReplyAsync(string text)
        {
            ShowTyping(true);
            // Delay scales with message length but stays in 400–1800 ms range
            int delay = Math.Clamp(text.Length * 2, 400, 1800);
            await Task.Delay(delay);
            ShowTyping(false);
            AppendBubble(text, isUser: false);
            UpdateMemoryPanel();
        }

        private void ShowTyping(bool show) =>
            Dispatcher.Invoke(() =>
                TypingRow.Visibility = show ? Visibility.Visible : Visibility.Collapsed);

        /// <summary>
        /// Central handler — appends the user's message then asks BotEngine to process it.
        /// Also manages the sentiment badge, scenario panel, and task list refresh.
        /// </summary>
        private async void SendUserMessage(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;

            string input = raw.Trim();
            AppendBubble(input, isUser: true);
            InputBox.Clear();
            InputBox_TextChanged(InputBox, null!);   // reset Send button + placeholder

            string response = _bot.ProcessInput(input,
                                                out string? _,
                                                out var sentiment);

            // Sentiment badge
            if (sentiment != EmotionAnalyzer.Sentiment.Neutral)
                ShowSentimentBadge(sentiment);

            // Sync scenario panel
            if (_bot.HasActiveScenario())
                ShowScenarioPanel(_bot.GetActiveScenario()!);
            else
                HideScenarioPanel();

            // Keep task list fresh when Tasks tab is visible
            if (PanelTasks.Visibility == Visibility.Visible)
                RefreshTaskList();

            await AddBotReplyAsync(response);
        }

        /// <summary>Shows the sentiment badge in the top bar for 3 seconds.</summary>
        private void ShowSentimentBadge(EmotionAnalyzer.Sentiment sentiment)
        {
            Dispatcher.Invoke(() =>
            {
                SentimentLabel.Text = _bot.Sentiment.GetLabel(sentiment);
                SentimentEmoji.Text = _bot.Sentiment.GetEmoji(sentiment);
                SentimentBar.Background =
                    (Brush)new BrushConverter().ConvertFromString(
                        _bot.Sentiment.GetStatusBarColour(sentiment))!;
                SentimentBar.Visibility = Visibility.Visible;

                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                timer.Tick += (_, _) => { SentimentBar.Visibility = Visibility.Collapsed; timer.Stop(); };
                timer.Start();
            });
        }

        // ── Scenario panel (embedded in Chat tab) ─────────────────────────────

        private void ShowScenarioPanel(SecurityChallenge challenge)
        {
            Dispatcher.Invoke(() =>
            {
                _activeScenario   = challenge;
                ScenarioDesc.Text = challenge.Description;
                ScenarioOptionsPanel.Children.Clear();

                for (int i = 0; i < challenge.Options.Count; i++)
                {
                    int captured = i;
                    var btn = new Button
                    {
                        Content = $"{(char)('A' + i)})  {challenge.Options[i]}",
                        Style   = (Style)FindResource("QuizOptionButton"),
                        Tag     = captured
                    };
                    btn.Click += ScenarioOption_Click;
                    ScenarioOptionsPanel.Children.Add(btn);
                }

                ScenarioPanelBox.Visibility = Visibility.Visible;
            });
        }

        private void HideScenarioPanel() =>
            Dispatcher.Invoke(() =>
            {
                ScenarioPanelBox.Visibility = Visibility.Collapsed;
                _activeScenario = null;
            });

        private void ScenarioOption_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not int idx) return;
            HideScenarioPanel();
            // Translate 0-based index back to letter for BotEngine
            SendUserMessage(((char)('A' + idx)).ToString());
        }

        private void CancelScenario_Click(object sender, RoutedEventArgs e)
        {
            HideScenarioPanel();
            _bot.CancelScenario();
            _ = AddBotReplyAsync("Scenario cancelled. Ask me anything else! 😊");
        }

        // ── Chat input event handlers ─────────────────────────────────────────

        private void SendButton_Click(object sender, RoutedEventArgs e)
            => SendUserMessage(InputBox.Text);

        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) SendUserMessage(InputBox.Text);
        }

        private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            bool hasText       = !string.IsNullOrWhiteSpace(InputBox.Text);
            SendBtn.IsEnabled  = hasText;
            Placeholder.Visibility = hasText ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ClearInput_Click(object sender, RoutedEventArgs e)
        {
            InputBox.Clear();
            InputBox.Focus();
        }

        // ── Memory panel ──────────────────────────────────────────────────────

        private void UpdateMemoryPanel()
        {
            Dispatcher.Invoke(() =>
            {
                UserBadge.Text = string.IsNullOrEmpty(_bot.Memory.UserName)
                    ? "Welcome!"
                    : $"Hi, {_bot.Memory.UserName}! 👋";

                MemLastTopic.Text = string.IsNullOrEmpty(_bot.Memory.LastTopic)
                    ? "Last topic: —"
                    : $"Last topic: {_bot.Memory.LastTopic}";

                MemInterests.Text = _bot.Memory.Interests.Count > 0
                    ? $"Interests: {_bot.Memory.GetInterestSummary()}"
                    : "Interests: —";
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        //  TASK ASSISTANT PANEL  (Task 1)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Reloads the task ListView from the database.</summary>
        private void RefreshTaskList()
        {
            if (_bot.DB == null)
            {
                TaskStatusLabel.Text = "⚠ Database unavailable — tasks require MySQL.";
                return;
            }

            try
            {
                var tasks = _bot.DB.GetTasks(_showCompletedTasks);

                // Project to display-only anonymous type for data binding
                TaskListView.ItemsSource = tasks.Select(t => new
                {
                    t.Id,
                    t.Title,
                    Description = t.Description ?? "—",
                    Created     = t.Created.ToString("yyyy-MM-dd HH:mm"),
                    Reminder    = t.Reminder.HasValue ? t.Reminder.Value.ToString("yyyy-MM-dd") : "—",
                    Status      = t.IsCompleted ? "✔ Done" : "⬜ Pending"
                }).ToList();

                int count = tasks.Count;
                TaskStatusLabel.Text = count == 0
                    ? "No tasks found. Add one above!"
                    : $"{count} task(s) loaded.";
            }
            catch (Exception ex)
            {
                TaskStatusLabel.Text = $"Error: {ex.Message}";
            }
        }

        private void AddTask_Click(object sender, RoutedEventArgs e)
        {
            string title = TaskTitleBox.Text.Trim();
            if (string.IsNullOrEmpty(title))
            {
                TaskStatusLabel.Text = "⚠ Please enter a task title.";
                TaskTitleBox.Focus();
                return;
            }

            if (_bot.DB == null)
            {
                TaskStatusLabel.Text = "⚠ Database unavailable — MySQL required.";
                return;
            }

            string?   desc     = string.IsNullOrWhiteSpace(TaskDescBox.Text) ? null : TaskDescBox.Text.Trim();
            DateTime? reminder = TaskReminderPicker.SelectedDate;

            try
            {
                int id = _bot.DB.AddTask(title, desc, reminder);
                _bot.ActivityLog.Log(
                    $"Task added: '{title}' (ID {id})" +
                    (reminder.HasValue ? $", reminder {reminder.Value:yyyy-MM-dd}" : ""));

                TaskStatusLabel.Text = $"✔ Task '{title}' added successfully (ID {id}).";
                TaskTitleBox.Clear();
                TaskDescBox.Clear();
                TaskReminderPicker.SelectedDate = null;
                RefreshTaskList();
            }
            catch (Exception ex)
            {
                TaskStatusLabel.Text = $"Error adding task: {ex.Message}";
            }
        }

        private void MarkTaskComplete_Click(object sender, RoutedEventArgs e)
        {
            if (TaskListView.SelectedItem is not { } item)
            {
                TaskStatusLabel.Text = "⚠ Select a task from the list first.";
                return;
            }

            int id = GetIntProp(item, "Id");
            if (_bot.DB == null) return;

            try
            {
                bool ok = _bot.DB.MarkTaskCompleted(id);
                _bot.ActivityLog.Log($"Task {id} marked completed.");
                TaskStatusLabel.Text = ok
                    ? $"✔ Task {id} marked as complete! Well done. 🎉"
                    : $"Task {id} not found.";
                RefreshTaskList();
            }
            catch (Exception ex) { TaskStatusLabel.Text = $"Error: {ex.Message}"; }
        }

        private void DeleteTask_Click(object sender, RoutedEventArgs e)
        {
            if (TaskListView.SelectedItem is not { } item)
            {
                TaskStatusLabel.Text = "⚠ Select a task from the list first.";
                return;
            }

            int    id    = GetIntProp(item, "Id");
            string title = GetStringProp(item, "Title");

            var confirm = MessageBox.Show(
                $"Are you sure you want to delete task '{title}'?",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;
            if (_bot.DB == null) return;

            try
            {
                bool ok = _bot.DB.DeleteTask(id);
                _bot.ActivityLog.Log($"Task {id} ('{title}') deleted.");
                TaskStatusLabel.Text = ok ? $"🗑 Task {id} deleted." : $"Task {id} not found.";
                RefreshTaskList();
            }
            catch (Exception ex) { TaskStatusLabel.Text = $"Error: {ex.Message}"; }
        }

        private void RefreshTasks_Click(object sender, RoutedEventArgs e) => RefreshTaskList();

        private void ToggleCompleted_Click(object sender, RoutedEventArgs e)
        {
            _showCompletedTasks    = !_showCompletedTasks;
            ToggleCompletedBtn.Content = _showCompletedTasks ? "✔ Hide Completed" : "✔ Show Completed";
            RefreshTaskList();
        }

        /// <summary>Reads an int property from an anonymous-type object via reflection.</summary>
        private static int GetIntProp(object obj, string prop) =>
            (int)obj.GetType().GetProperty(prop)!.GetValue(obj)!;

        private static string GetStringProp(object obj, string prop) =>
            (string)obj.GetType().GetProperty(prop)!.GetValue(obj)!;

        // ─────────────────────────────────────────────────────────────────────
        //  MINI-GAME QUIZ PANEL  (Task 2)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Initialises a new quiz session and shows the first question.</summary>
        private void StartQuiz_Click(object sender, RoutedEventArgs e)
        {
            _bot.Quiz.Start();
            _bot.ActivityLog.Log("Quiz game started.");

            // Reset UI
            QuizStartScreen.Visibility   = Visibility.Collapsed;
            QuizResultsPanel.Visibility  = Visibility.Collapsed;
            QuizQuestionPanel.Visibility = Visibility.Visible;

            // Configure progress bar dynamically
            QuizProgressBar.Maximum = _bot.Quiz.TotalQuestions;
            QuizProgressBar.Value   = 0;

            // Start elapsed timer
            _quizElapsedSeconds = 0;
            QuizTimerLabel.Text = "⏱ 0s elapsed";
            _quizTimer?.Stop();
            _quizTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _quizTimer.Tick += (_, _) =>
            {
                _quizElapsedSeconds++;
                QuizTimerLabel.Text = $"⏱ {_quizElapsedSeconds}s elapsed";
            };
            _quizTimer.Start();

            ShowNextQuestion();
        }

        /// <summary>Dequeues and renders the next quiz question, or ends the quiz.</summary>
        private void ShowNextQuestion()
        {
            var q = _bot.Quiz.GetNextQuestion();
            if (q == null)
            {
                EndQuiz();
                return;
            }

            _answerSubmitted = false;

            int qNum = _bot.Quiz.CurrentQuestionNumber;
            int tot  = _bot.Quiz.TotalQuestions;

            QuizProgressBar.Value   = qNum - 1;
            QuizQuestionNumber.Text = $"Question {qNum} of {tot}";
            QuizProgressLabel.Text  = $"Question {qNum} / {tot}";
            QuizQuestionText.Text   = q.Text;
            QuizScoreLabel.Text     = $"Score: {_bot.Quiz.CurrentScore} / {tot}";

            // Build answer buttons A / B / C / D (or A / B for True/False)
            QuizOptionsPanel.Children.Clear();
            for (int i = 0; i < q.Options.Count; i++)
            {
                int   idx    = i;
                char  letter = (char)('A' + i);
                var   btn    = new Button
                {
                    Content = $"{letter})  {q.Options[i]}",
                    Style   = (Style)FindResource("QuizOptionButton"),
                    Tag     = idx
                };
                btn.Click += QuizAnswer_Click;
                QuizOptionsPanel.Children.Add(btn);
            }

            QuizFeedbackBox.Visibility = Visibility.Collapsed;
            QuizNextBtn.Visibility     = Visibility.Collapsed;
        }

        /// <summary>Processes a player's answer selection.</summary>
        private void QuizAnswer_Click(object sender, RoutedEventArgs e)
        {
            if (_answerSubmitted) return;
            if (sender is not Button btn || btn.Tag is not int idx) return;

            _answerSubmitted = true;

            // Disable all answer buttons to prevent re-selection
            foreach (var child in QuizOptionsPanel.Children.OfType<Button>())
                child.IsEnabled = false;

            var (correct, label, _) = _bot.Quiz.SubmitAnswer(idx);
            _bot.ActivityLog.Log(
                $"Quiz Q{_bot.Quiz.CurrentQuestionNumber}: {(correct ? "Correct" : "Wrong")}");

            // Colour the selected button
            btn.Background = correct
                ? new SolidColorBrush(Color.FromRgb(0xD4, 0xED, 0xDA))   // soft green
                : new SolidColorBrush(Color.FromRgb(0xF8, 0xD7, 0xDA));   // soft red

            // Show feedback — label (✅/❌) and the plain explanation from QuizEngine
            QuizFeedbackText.Text     = label;
            QuizExplanationText.Text  = _bot.Quiz.LastExplanation;   // no emoji prefix to strip
            QuizFeedbackBox.Background = correct
                ? new SolidColorBrush(Color.FromRgb(0xD4, 0xED, 0xDA))
                : new SolidColorBrush(Color.FromRgb(0xF8, 0xD7, 0xDA));
            QuizFeedbackBox.Visibility = Visibility.Visible;

            QuizScoreLabel.Text    = $"Score: {_bot.Quiz.CurrentScore} / {_bot.Quiz.TotalQuestions}";
            QuizNextBtn.Visibility = Visibility.Visible;
        }

        private void QuizNext_Click(object sender, RoutedEventArgs e) => ShowNextQuestion();

        /// <summary>Finalises the quiz session and shows the results screen.</summary>
        private void EndQuiz()
        {
            _quizTimer?.Stop();

            var (score, total, msg) = _bot.Quiz.GetFinalResult();
            _bot.ActivityLog.Log($"Quiz completed. Score: {score}/{total} in {_quizElapsedSeconds}s.");

            QuizQuestionPanel.Visibility = Visibility.Collapsed;

            int pct = total > 0 ? score * 100 / total : 0;
            QuizResultsEmoji.Text = pct >= 92 ? "🏆" : pct >= 75 ? "🎉" : pct >= 58 ? "👍" : "📚";
            QuizResultsScore.Text = $"{score} / {total}  ({pct}%)";
            QuizResultsMsg.Text   = msg + $"\n\nTime: {_quizElapsedSeconds} seconds";

            QuizProgressLabel.Text  = "Quiz complete!";
            QuizProgressBar.Value   = total;
            QuizScoreLabel.Text     = $"Final: {score} / {total}";

            QuizResultsPanel.Visibility = Visibility.Visible;
        }

        private void QuitQuiz_Click(object sender, RoutedEventArgs e)
        {
            _quizTimer?.Stop();
            _bot.Quiz.Reset();
            _bot.ActivityLog.Log("Quiz quit by user.");

            QuizQuestionPanel.Visibility = Visibility.Collapsed;
            QuizResultsPanel.Visibility  = Visibility.Collapsed;
            QuizStartScreen.Visibility   = Visibility.Visible;
            QuizProgressLabel.Text = "Press Start to begin!";
            QuizScoreLabel.Text    = "Score: 0 / 0";
            QuizTimerLabel.Text    = "⏱ 0s elapsed";
        }

        // ─────────────────────────────────────────────────────────────────────
        //  ACTIVITY LOG PANEL  (Task 4)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Renders the current page of log entries.</summary>
        private void RefreshLogPanel()
        {
            var entries = _bot.ActivityLog.GetRecentLogs(_logPageSize);
            int total   = _bot.ActivityLog.TotalCount;

            RenderLogEntries(entries);
            LogCountLabel.Text = $"Showing {Math.Min(_logPageSize, total)} of {total} entries.";
            LogFooter.Text = _logPageSize < total
                ? $"Showing {_logPageSize} most recent entries. Click 'Show More' for the full history."
                : $"All {total} entries shown.";

            ShowMoreLogBtn.Content = _logPageSize >= total ? "Show Less" : "Show More";
        }

        private void RenderLogEntries(System.Collections.Generic.IEnumerable<string> entries)
        {
            LogPanel.Children.Clear();
            bool alternate = false;

            foreach (var entry in entries)
            {
                var row = new Border
                {
                    Background   = alternate
                        ? new SolidColorBrush(Color.FromRgb(0xFF, 0xF0, 0xF5))
                        : new SolidColorBrush(Colors.White),
                    Padding      = new Thickness(14, 8, 14, 8),
                    Margin       = new Thickness(0, 2, 0, 2),
                    CornerRadius = new CornerRadius(6)
                };
                row.Child = new TextBlock
                {
                    Text         = entry,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize     = 12,
                    Foreground   = (Brush)FindResource("TextPrimary")
                };
                LogPanel.Children.Add(row);
                alternate = !alternate;
            }

            if (!entries.Any())
            {
                LogPanel.Children.Add(new TextBlock
                {
                    Text       = "No activity recorded yet. Start chatting, add tasks, or play the quiz!",
                    FontSize   = 13,
                    Foreground = (Brush)FindResource("TextMuted"),
                    Margin     = new Thickness(0, 16, 0, 0)
                });
            }
        }

        private void RefreshLog_Click(object sender, RoutedEventArgs e) => RefreshLogPanel();

        private void ShowMoreLog_Click(object sender, RoutedEventArgs e)
        {
            int total = _bot.ActivityLog.TotalCount;
            _logPageSize = _logPageSize >= total ? 10 : Math.Min(_logPageSize + 10, total);
            RefreshLogPanel();
        }

        private void ExportLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string path    = Path.Combine(desktop,
                    $"CyberInfo_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

                var all = _bot.ActivityLog.GetAllLogs();
                File.WriteAllLines(path, all);

                _bot.ActivityLog.Log($"Activity log exported ({all.Count} entries).");
                LogCountLabel.Text = $"✔ Exported {all.Count} entries to Desktop.";
                RefreshLogPanel();
            }
            catch (Exception ex)
            {
                LogCountLabel.Text = $"Export failed: {ex.Message}";
            }
        }
    }
}
