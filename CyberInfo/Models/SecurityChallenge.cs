using System.Collections.Generic;

namespace CyberInfo.Models
{
    /// <summary>Represents a scenario-based security challenge question.</summary>
    public class SecurityChallenge
    {
        public string Description      { get; }
        public List<string> Options    { get; }
        public int CorrectOptionIndex  { get; }
        public string Feedback         { get; }

        public SecurityChallenge(string desc, List<string> opts, int correct, string feedback)
        {
            Description        = desc;
            Options            = opts;
            CorrectOptionIndex = correct;
            Feedback           = feedback;
        }
    }
}
