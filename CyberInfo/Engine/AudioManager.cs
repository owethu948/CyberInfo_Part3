using System;
using System.IO;
using System.Media;
using System.Threading.Tasks;

namespace CyberInfo.Engine
{
    /// <summary>Plays the greeting audio file asynchronously on first launch.</summary>
    public static class AudioManager
    {
        private static readonly string WavPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Greeting-AI.wav");

        /// <summary>Plays the greeting sound in a background thread (non-blocking).</summary>
        public static void PlayGreetingAsync()
        {
            Task.Run(() =>
            {
                try
                {
                    if (File.Exists(WavPath))
                    {
                        using var player = new SoundPlayer(WavPath);
                        player.PlaySync();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Audio] Could not play greeting: {ex.Message}");
                }
            });
        }
    }
}
