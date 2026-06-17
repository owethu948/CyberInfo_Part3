namespace CyberInfo.Models
{
    /// <summary>
    /// Cybersecurity topic with auto-properties.
    /// Provides a pre-formatted GUI-friendly response string.
    /// </summary>
    public class SecurityTopic
    {
        public string Name         { get; }
        public string WhatIs       { get; }
        public string WhyMatters   { get; }
        public string WhenWhere    { get; }
        public string Prevention   { get; }
        public string Reference    { get; }

        public SecurityTopic(string name, string whatIs, string whyMatters,
                             string whenWhere, string prevention, string reference)
        {
            Name       = name;
            WhatIs     = whatIs;
            WhyMatters = whyMatters;
            WhenWhere  = whenWhere;
            Prevention = prevention;
            Reference  = reference;
        }

        /// <summary>Returns a multi-section formatted chat reply for the GUI.</summary>
        public string BuildGuiResponse() =>
            $"📘  {Name.ToUpper()}\n" +
            $"{"─".PadRight(55, '─')}\n\n" +
            $"❓  WHAT IS {Name.ToUpper()}?\n{WhatIs}\n\n" +
            $"⚠️   WHY IT MATTERS:\n{WhyMatters}\n\n" +
            $"📍  WHEN & WHERE YOU'RE AT RISK:\n{WhenWhere}\n\n" +
            $"🛡️   HOW TO PROTECT YOURSELF:\n{Prevention}\n\n" +
            $"📖  Source: {Reference}";
    }
}
