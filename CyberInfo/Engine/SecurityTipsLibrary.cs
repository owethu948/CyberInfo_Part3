using System;
using System.Collections.Generic;

namespace CyberInfo.Engine
{
    /// <summary>
    /// Provides a randomised pool of security tips for each cybersecurity topic.
    /// </summary>
    public class SecurityTipsLibrary
    {
        public delegate string ResponseSelector(string topic);
        public readonly Func<string, string> GetRandomTip;
        public event Action<string, string>? OnTipSelected;

        private readonly Dictionary<string, List<string>> _pools =
            new(StringComparer.OrdinalIgnoreCase)
        {
            ["phishing"] = new()
            {
                "🎣 Watch for emails with urgent language like 'Act now!' Legitimate organisations rarely demand immediate action.",
                "🎣 Always check the sender's full email address — criminals use look-alike domains such as 'amaz0n.com'.",
                "🎣 Never click links in unexpected emails. Open your browser and type the website address directly.",
                "🎣 Real banks and SARS will NEVER ask for your password, OTP, or PIN via email or SMS.",
                "🎣 If an email feels off, forward it to the organisation's official abuse address before deleting."
            },
            ["password"] = new()
            {
                "🔑 Use at least 16 random characters mixing uppercase, lowercase, numbers, and symbols.",
                "🔑 Never reuse the same password across sites. One breach exposes every account sharing that password.",
                "🔑 Use a reputable password manager like Bitwarden (free) to generate and store unique passwords.",
                "🔑 Enable two-factor authentication (2FA) everywhere — even a weak password becomes far safer.",
                "🔑 Check haveibeenpwned.com to see if your email has appeared in a known data breach."
            },
            ["scam"] = new()
            {
                "🚨 If an offer sounds too good to be true, it almost certainly is a scam.",
                "🚨 Impersonation scams are surging in SA — always call the official number if someone claims to be from your bank.",
                "🚨 Advance-fee scams ask for upfront payment to receive a larger reward. Legitimate jobs never require payment.",
                "🚨 Report scams to SAPS and SABRIC at sabric.co.za.",
                "🚨 Slow down when someone creates urgency — scammers rely on panic to stop critical thinking."
            },
            ["privacy"] = new()
            {
                "🔒 Review privacy settings on every social media account. Limit who can see your posts.",
                "🔒 Be cautious about personal details shared online — criminals piece together profiles from multiple sources.",
                "🔒 Use a VPN when connecting to public Wi-Fi to encrypt your traffic.",
                "🔒 Read app permissions carefully — a torch app requesting contacts access is a red flag.",
                "🔒 Fewer accounts = fewer leaks. Use 'Sign in with Google/Apple' where possible."
            },
            ["malware"] = new()
            {
                "🦠 Keep all software updated — most malware exploits holes that have already been patched.",
                "🦠 Download software only from official websites or verified app stores.",
                "🦠 Back up your files regularly. Ransomware is far less devastating with clean backups.",
                "🦠 Reputable antivirus tools (Windows Defender, Bitdefender) provide real-time protection.",
                "🦠 Never plug in a USB drive you found or received unexpectedly — 'USB drop' attacks are real."
            },
            ["wifi"] = new()
            {
                "📶 Avoid online banking or email login over public Wi-Fi — the network may be monitored.",
                "📶 Ask staff for the exact Wi-Fi name. Criminals set up look-alike hotspots.",
                "📶 A trusted VPN (ProtonVPN, NordVPN) encrypts all traffic on public networks.",
                "📶 Use your phone's mobile data hotspot instead of public Wi-Fi for secure tasks.",
                "📶 Set your device to forget public Wi-Fi networks after use to prevent auto-reconnection."
            },
            ["2fa"] = new()
            {
                "🔐 2FA means a stolen password alone is not enough — the attacker still needs the second factor.",
                "🔐 Use an authenticator app (Google/Microsoft Authenticator) rather than SMS — SIM-swap fraud can intercept SMS.",
                "🔐 Save your 2FA backup codes in a secure offline location.",
                "🔐 Enable 2FA on your email first — it's the master key to every password-reset flow.",
                "🔐 Hardware security keys (YubiKey) offer the strongest 2FA and are immune to phishing."
            },
            ["social engineering"] = new()
            {
                "🎭 Attackers manipulate people, not machines. Verify any caller's identity before sharing info.",
                "🎭 If someone claims to be from IT or your bank and asks for credentials, hang up and call the official number.",
                "🎭 Pretexting: attackers create convincing backstories. Always verify before acting.",
                "🎭 Train yourself to pause before reacting to urgent requests. Urgency is the scammer's most powerful tool.",
                "🎭 Vishing (voice phishing) is rising in SA — scammers spoof caller IDs to appear as legitimate organisations."
            },
            ["updates"] = new()
            {
                "🔄 Enable automatic updates for Windows, macOS, Android, and iOS — they patch critical security holes.",
                "🔄 Never postpone updates indefinitely; many fix zero-day vulnerabilities actively exploited by attackers.",
                "🔄 Routers and smart devices need firmware updates too — check the manufacturer's site regularly.",
                "🔄 Unpatched software is the single most common way criminals break into home computers.",
                "🔄 Restart after updates — many patches don't take effect until the device reboots."
            },
            ["safe browsing"] = new()
            {
                "🌐 Look for 🔒 and 'https://' before entering any personal or payment information.",
                "🌐 Avoid downloading files from unknown sites — they're a primary malware delivery vector.",
                "🌐 Use uBlock Origin browser extension to block dangerous ads and pop-ups.",
                "🌐 Be wary of shortened URLs (bit.ly, tinyurl) — you can't see the destination without clicking.",
                "🌐 A browser warning about a certificate or unsafe site is there for a reason — don't click through."
            }
        };

        private readonly Random _rand = new();

        public SecurityTipsLibrary()
        {
            GetRandomTip = topic =>
            {
                if (_pools.TryGetValue(topic, out var pool) && pool.Count > 0)
                {
                    string r = pool[_rand.Next(pool.Count)];
                    OnTipSelected?.Invoke(topic, r);
                    return r;
                }
                return "Stay vigilant and keep learning — awareness is your best defence! 🛡️";
            };
        }

        public ResponseSelector CreateSelectorFor(string topic) => _ => GetRandomTip(topic);
        public bool HasTopic(string topic) => _pools.ContainsKey(topic);
    }
}
