using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Verse;

namespace ModCompatChecker.Core
{
    /// <summary>
    /// Post-processes AI responses to detect potentially dangerous suggestions.
    /// Pure local analysis — no additional API cost.
    /// </summary>
    public static class AIResponseAuditor
    {
        private static readonly List<AuditRule> _rules = new List<AuditRule>
        {
            // File modification patterns
            new AuditRule("file_write", AuditSeverity.High,
                @"File\.Write(AllText|AllBytes|AllLines)?\s*\(", "Suggested file write operation"),
            new AuditRule("file_delete", AuditSeverity.High,
                @"File\.Delete\s*\(|Directory\.Delete\s*\(", "Suggested file/directory deletion"),
            new AuditRule("file_move", AuditSeverity.Medium,
                @"File\.Move\s*\(|Directory\.Move\s*\(", "Suggested file move operation"),
            
            // Code execution patterns
            new AuditRule("code_exec", AuditSeverity.Critical,
                @"Process\.Start\s*\(|System\.Diagnostics\.Process|Runtime\.GetRuntime\(\)\.exec",
                "Suggested code/process execution"),
            new AuditRule("reflection", AuditSeverity.Critical,
                @"Assembly\.Load|System\.Reflection|Activator\.CreateInstance|\.Invoke\s*\(",
                "Suggested reflection/dynamic code loading"),
            
            // Harmony patching
            new AuditRule("harmony_inject", AuditSeverity.Critical,
                @"new\s+Harmony\s*\(|HarmonyInstance\.Create|\.PatchAll\s*\(|\.CreateAndPatch",
                "Suggested Harmony patch injection"),
            
            // Dangerous system operations
            new AuditRule("registry", AuditSeverity.High,
                @"Registry\.(LocalMachine|CurrentUser|ClassesRoot)|Microsoft\.Win32\.Registry",
                "Suggested registry modification"),
            new AuditRule("powershell", AuditSeverity.High,
                @"powershell\s+(-Command|-EncodedCommand|Invoke-Expression)|cmd\.exe\s+/c",
                "Suggested shell command execution"),
            
            // Mod file modification
            new AuditRule("mod_xml", AuditSeverity.Medium,
                @"修改.*About\.xml|修改.*Defs\|修改.*Patches\|edit.*Mods\\",
                "Suggested mod file modification"),
        };

        /// <summary>
        /// Audit an AI response. Returns list of triggered rules.
        /// </summary>
        public static List<AuditFinding> Audit(string response)
        {
            var findings = new List<AuditFinding>();
            if (string.IsNullOrEmpty(response)) return findings;

            foreach (var rule in _rules)
            {
                try
                {
                    var match = Regex.Match(response, rule.Pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
                    if (match.Success)
                    {
                        findings.Add(new AuditFinding
                        {
                            Rule = rule,
                            MatchedText = TruncateMatch(match.Value),
                            Timestamp = DateTime.Now
                        });
                    }
                }
                catch (RegexMatchTimeoutException) { }
            }
            return findings;
        }

        /// <summary>
        /// Quick check — returns true if any high/critical rules triggered.
        /// </summary>
        public static bool HasDangerousContent(string response)
        {
            var findings = Audit(response);
            foreach (var f in findings)
                if (f.Rule.Severity >= AuditSeverity.High)
                    return true;
            return false;
        }

        public static string BuildAuditWarning(List<AuditFinding> findings)
        {
            if (findings.Count == 0) return "";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[ModCompatChecker] " + "ModCompatChecker.SelfAuditHeader".Translate());
            sb.AppendLine("ModCompatChecker.SelfAuditBody".Translate());
            foreach (var f in findings)
            {
                sb.AppendLine(string.Format("  [{0}] {1}: \"{2}\"", f.Rule.Severity, f.Rule.Description, f.MatchedText));
            }
            sb.AppendLine("ModCompatChecker.SelfAuditFooter1".Translate());
            sb.AppendLine("ModCompatChecker.SelfAuditFooter2".Translate());
            return sb.ToString();
        }

        private static string TruncateMatch(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Length > 80 ? text.Substring(0, 77) + "..." : text;
        }
    }

    public enum AuditSeverity
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Critical = 3
    }

    public class AuditRule
    {
        public string Id;
        public AuditSeverity Severity;
        public string Pattern;
        public string Description;
        public AuditRule(string id, AuditSeverity severity, string pattern, string description)
        {
            Id = id; Severity = severity; Pattern = pattern; Description = description;
        }
    }

    public class AuditFinding
    {
        public AuditRule Rule;
        public string MatchedText;
        public DateTime Timestamp;
    }
}
