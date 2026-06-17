# CyberInfo – Cybersecurity Awareness Chatbot (Part 3 / POE)

A polished WPF (.NET 10) desktop application that educates users on cybersecurity topics
through an interactive chatbot, task manager, quiz game, and activity log.

---

## Quick Start

### Prerequisites
| Tool | Version | Download |
|------|---------|----------|
| Visual Studio | 2022 v17.8+ | https://visualstudio.microsoft.com |
| .NET SDK | 10.0 | included with VS 2022 |
| MySQL Server | 8.0+ (optional) | https://dev.mysql.com/downloads/mysql/ |

### Open & Run
1. Unzip `CyberInfo_Part3.zip`
2. Double-click `CyberInfo.slnx` → opens in Visual Studio 2022
3. Press **F5** or click **▶ Start**

> The app runs without MySQL. If MySQL is unavailable, the Task Assistant
> tab shows a warning and chat-based task commands are disabled gracefully.

---

## MySQL Setup (Task 1 – Task Assistant)

If MySQL Server is installed with default settings (root / no password):
- **No configuration needed** — the app creates the `cyberinfo` database and `tasks`
  table automatically on first run.

To use different credentials, open `Engine/DatabaseManager.cs` and update:
```csharp
private const string DefaultConnection =
    "Server=localhost;Database=cyberinfo;Uid=root;Pwd=;CharSet=utf8mb4;";
```

---

## Features

### 💬 Chat (NLP Simulation – Task 3)
- Detects intents from natural language: greetings, farewells, help requests,
  task commands, quiz launch, and cybersecurity topics
- Handles synonym variations via `ThreatRecognizer` keyword maps
- Sentiment analysis (worried / curious / frustrated / happy) with visual badge
- Eight topic deep-dives: Passwords, Phishing, Safe Browsing, 2FA, Malware,
  Social Engineering, Software Updates, Public Wi-Fi
- Scenario challenges, random facts, and rotating security tips
- Full quiz playable in-chat (A/B/C/D answers)
- Session memory: remembers your name, last topic, and expressed interests

### 📋 Task Assistant (Task 1)
- Add, view, mark complete, and delete cybersecurity tasks
- Optional reminder date via date picker
- Toggle to show/hide completed tasks
- Tasks saved to MySQL (`cyberinfo.tasks` table)
- Also addable via chat: `add task to enable 2FA`

### 🎮 Quiz Game (Task 2)
- 12 questions (10 multiple-choice + 2 True/False), shuffled each session
- Progress bar, per-question timer, immediate colour-coded feedback
- Per-question explanations written by the engine
- Score percentage + motivational results screen
- Elapsed time tracked

### 📜 Activity Log (Task 4)
- Every significant bot action timestamped and recorded
- Paginated display (10 / 20 / 30 … entries)
- Export to timestamped `.txt` file on Desktop
- Also viewable via chat: `show activity log`

---

## Project Structure

```
CyberInfo/
├── CyberInfo.slnx
└── CyberInfo/
    ├── CyberInfo.csproj
    ├── App.xaml / App.xaml.cs
    ├── AssemblyInfo.cs
    ├── MainWindow.xaml          ← UI layout (4 panels)
    ├── MainWindow.xaml.cs       ← Code-behind / event handlers
    ├── Engine/
    │   ├── BotEngine.cs         ← Central orchestrator
    │   ├── NLPProcessor.cs      ← Intent detection (Task 3)
    │   ├── QuizEngine.cs        ← Quiz logic (Task 2)
    │   ├── DatabaseManager.cs   ← MySQL CRUD (Task 1)
    │   ├── ActivityLogger.cs    ← Event logging (Task 4)
    │   ├── EmotionAnalyzer.cs   ← Sentiment detection
    │   ├── ThreatRecognizer.cs  ← Keyword → topic mapping
    │   ├── SecurityTipsLibrary.cs
    │   ├── AudioManager.cs
    │   └── UserProfile.cs       ← Session memory
    ├── Models/
    │   ├── Message.cs
    │   ├── SecurityChallenge.cs
    │   └── SecurityTopic.cs
    └── Resources/
        └── Greeting-AI.wav
```

---

## NuGet Packages
| Package | Purpose |
|---------|---------|
| `MySql.Data 9.3.0` | MySQL task persistence |
| `System.Windows.Extensions 10.0.8` | SoundPlayer for WAV greeting |

Visual Studio restores these automatically on first build (internet required).

---

*IIE – PROG6221 POE Part 3 · 2025*

