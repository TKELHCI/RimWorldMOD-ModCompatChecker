using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using ModCompatChecker.AI;
using UnityEngine;
using Verse;

namespace ModCompatChecker.Core
{
    public class ErrorEntry
    {
        public string Id;
        public string Keyword;
        public string Pattern;
        public string Category;
        public string Severity;
        public string ExplanationZh;
        public string ExplanationEn;
    }

    public static class ErrorEncyclopedia
    {
        private static List<ErrorEntry> _entries;
        private static string _userOverridesPath;
        private static bool _initialized;

        public static List<ErrorEntry> Entries
        {
            get { if (!_initialized) LoadFromJson(); return _entries; }
        }

        public static string UserOverridesPath
        {
            get
            {
                if (_userOverridesPath == null)
                    _userOverridesPath = Path.Combine(GenFilePaths.ConfigFolderPath, "ModCompatChecker_Encyclopedia.json");
                return _userOverridesPath;
            }
        }

        /// <summary>
        /// Load official entries from JSON, then merge user overrides on top.
        /// User-modified entries are never overwritten by official updates.
        /// </summary>
        public static void LoadFromJson()
        {
            _initialized = true;
            var official = LoadOfficialEntries();
            var user = LoadUserOverrides();

            var merged = new List<ErrorEntry>();
            foreach (var off in official)
            {
                if (user.TryGetValue(off.Id, out var ue))
                    merged.Add(ue); // User override wins
                else
                    merged.Add(off); // Official entry
            }
            // Add entirely new user entries
            foreach (var kv in user)
            {
                if (!official.Exists(o => o.Id == kv.Key))
                    merged.Add(kv.Value);
            }
            _entries = merged;
        }

        private static List<ErrorEntry> LoadOfficialEntries()
        {
            try
            {
                foreach (var mod in LoadedModManager.RunningMods)
                {
                    var jsonPath = Path.Combine(mod.RootDir, "Assemblies", "ErrorEncyclopedia.json");
                    if (!File.Exists(jsonPath)) continue;
                    var json = File.ReadAllText(jsonPath);
                    return SimpleJsonParse(json);
                }
            }
            catch { }
            // Fallback to built-in minimal set
            return GetBuiltInEntries();
        }

        private static Dictionary<string, ErrorEntry> LoadUserOverrides()
        {
            var dict = new Dictionary<string, ErrorEntry>();
            try
            {
                if (File.Exists(UserOverridesPath))
                {
                    var json = File.ReadAllText(UserOverridesPath);
                    var list = SimpleJsonParse(json);
                    foreach (var e in list) dict[e.Id] = e;
                }
            }
            catch { }
            return dict;
        }

        /// <summary>
        /// Save user-defined/customized entries. Only stores overrides, not full set.
        /// </summary>
        public static void SaveUserOverrides()
        {
            try
            {
                var official = LoadOfficialEntries();
                var overrides = new List<ErrorEntry>();
                foreach (var e in _entries)
                {
                    var off = official.Find(o => o.Id == e.Id);
                    if (off == null || EntryDiffers(e, off))
                        overrides.Add(e);
                }
                var json = SimpleJsonStringify(overrides);
                File.WriteAllText(UserOverridesPath, json);
            }
            catch (Exception ex) { Log.Warning("[ModCompatChecker] Save encyclopedia failed: " + ex.Message); }
        }

        private static bool EntryDiffers(ErrorEntry a, ErrorEntry b)
        {
            return a.Keyword != b.Keyword || a.Pattern != b.Pattern || a.Category != b.Category ||
                   a.Severity != b.Severity || a.ExplanationZh != b.ExplanationZh || a.ExplanationEn != b.ExplanationEn;
        }

        /// <summary>
        /// Reset a single entry to official default.
        /// </summary>
        public static void ResetEntry(string id)
        {
            var official = LoadOfficialEntries();
            var off = official.Find(o => o.Id == id);
            if (off != null)
            {
                _entries.RemoveAll(e => e.Id == id);
                _entries.Add(off);
                SaveUserOverrides();
            }
        }

        /// <summary>
        /// Add or update a user entry. Returns false on duplicate id.
        /// </summary>
        public static bool SaveEntry(ErrorEntry entry)
        {
            if (string.IsNullOrEmpty(entry.Id)) return false;
            _entries.RemoveAll(e => e.Id == entry.Id);
            _entries.Add(entry);
            SaveUserOverrides();
            return true;
        }

        /// <summary>
        /// Delete a user-added entry. Official entries cannot be deleted, only reset.
        /// </summary>
        public static bool DeleteEntry(string id)
        {
            var official = LoadOfficialEntries();
            if (official.Exists(o => o.Id == id)) return false; // Can't delete official
            _entries.RemoveAll(e => e.Id == id);
            SaveUserOverrides();
            return true;
        }

        public static List<(ErrorEntry Entry, Match Match)> MatchError(string errorText)
        {
            var results = new List<(ErrorEntry, Match)>();
            if (string.IsNullOrEmpty(errorText)) return results;
            foreach (var entry in Entries)
            {
                try
                {
                    var match = Regex.Match(errorText, entry.Pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
                    if (match.Success) results.Add((entry, match));
                }
                catch (RegexMatchTimeoutException) { }
            }
            return results;
        }

        public static string GetExplanation(ErrorEntry entry)
        {
            var lang = PromptBuilder.GetPromptLanguage();
            return lang == "zh" ? entry.ExplanationZh : entry.ExplanationEn;
        }

        public static Color GetSeverityColor(string severity)
        {
            switch (severity)
            {
                case "critical": return new Color(0.9f, 0.2f, 0.2f);
                case "high": return new Color(0.9f, 0.5f, 0.1f);
                case "medium": return new Color(0.9f, 0.8f, 0.1f);
                case "low": return new Color(0.5f, 0.7f, 0.5f);
                default: return Color.grey;
            }
        }

        // ── Simple JSON parser (avoids needing Newtonsoft.Json) ──
        private static List<ErrorEntry> SimpleJsonParse(string json)
        {
            var entries = new List<ErrorEntry>();
            var matches = Regex.Matches(json, @"\{[^}]+\}");
            foreach (Match m in matches)
            {
                var block = m.Value;
                var e = new ErrorEntry();
                e.Id = ExtractJsonStr(block, "id");
                e.Keyword = ExtractJsonStr(block, "keyword");
                e.Pattern = ExtractJsonStr(block, "pattern");
                e.Category = ExtractJsonStr(block, "category");
                e.Severity = ExtractJsonStr(block, "severity");
                e.ExplanationZh = ExtractJsonStr(block, "explanationZh");
                e.ExplanationEn = ExtractJsonStr(block, "explanationEn");
                if (!string.IsNullOrEmpty(e.Id)) entries.Add(e);
            }
            return entries;
        }

        private static string SimpleJsonStringify(List<ErrorEntry> entries)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[");
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                sb.AppendLine("  {");
                sb.AppendLine($"    \"id\": \"{EscapeJsonStr(e.Id)}\",");
                sb.AppendLine($"    \"keyword\": \"{EscapeJsonStr(e.Keyword)}\",");
                sb.AppendLine($"    \"pattern\": \"{EscapeJsonStr(e.Pattern)}\",");
                sb.AppendLine($"    \"category\": \"{EscapeJsonStr(e.Category)}\",");
                sb.AppendLine($"    \"severity\": \"{EscapeJsonStr(e.Severity)}\",");
                sb.AppendLine($"    \"explanationZh\": \"{EscapeJsonStr(e.ExplanationZh)}\",");
                sb.AppendLine($"    \"explanationEn\": \"{EscapeJsonStr(e.ExplanationEn)}\"");
                sb.Append(i < entries.Count - 1 ? "  }," : "  }");
                sb.AppendLine();
            }
            sb.AppendLine("]");
            return sb.ToString();
        }

        private static string ExtractJsonStr(string json, string key)
        {
            var m = Regex.Match(json, $"\"{key}\"\\s*:\\s*\"([^\"]*)\"");
            return m.Success ? m.Groups[1].Value : "";
        }

        private static string EscapeJsonStr(string s)
        {
            return (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static List<ErrorEntry> GetBuiltInEntries()
        {
            return new List<ErrorEntry>
            {
                new ErrorEntry { Id="nullref", Keyword="NullReferenceException", Pattern=@"NullReferenceException", Category="crash", Severity="critical", ExplanationZh="空引用异常", ExplanationEn="Null reference exception" }
            };
        }
    }
}