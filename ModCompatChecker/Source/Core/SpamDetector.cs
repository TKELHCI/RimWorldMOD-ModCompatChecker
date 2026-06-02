using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using Verse;

namespace ModCompatChecker.Core
{
    public class SpamAlert
    {
        public string NormalizedMessage;
        public int Count;
        public bool IsActive;
    }

    public static class SpamDetector
    {
        private static readonly Dictionary<string, SpamAlert> _alerts = new Dictionary<string, SpamAlert>();
        private static float _lastCheckTime;
        private const float CheckInterval = 30f;
        private const int SpamThreshold = 30;

        public static bool PopupEnabled = false;
        public static bool HasNewAlert;
        public static readonly List<SpamAlert> ActiveAlerts = new List<SpamAlert>();

        public static void Reset()
        {
            _alerts.Clear();
            ActiveAlerts.Clear();
            HasNewAlert = false;
        }

        public static void CheckForSpam()
        {
            try
            {
                var now = Time.realtimeSinceStartup;
                if (now - _lastCheckTime < CheckInterval && _alerts.Count > 0) return;
                _lastCheckTime = now;

                var messages = Log.Messages;
                if (messages == null) return;

                var errCounts = new Dictionary<string, int>();
                var samples = new Dictionary<string, string>();

                foreach (var msg in messages)
                {
                    var text = msg.text ?? "";
                    var typeStr = msg.type.ToString();
                    if (!typeStr.Contains("Error") && !typeStr.Contains("Warning")) continue;
                    var norm = Normalize(text);
                    if (!errCounts.ContainsKey(norm)) { errCounts[norm] = 0; samples[norm] = Truncate(text); }
                    errCounts[norm]++;
                }

                ActiveAlerts.Clear();
                HasNewAlert = false;

                foreach (var kv in errCounts)
                {
                    if (kv.Value >= SpamThreshold)
                    {
                        if (!_alerts.ContainsKey(kv.Key)) { _alerts[kv.Key] = new SpamAlert(); HasNewAlert = true; }
                        var a = _alerts[kv.Key];
                        a.Count = kv.Value;
                        a.NormalizedMessage = samples[kv.Key] ?? kv.Key;
                        a.IsActive = true;
                        ActiveAlerts.Add(a);
                    }
                }
            }
            catch (Exception ex) { Log.Warning("[ModCompatChecker] SpamDetector: " + ex.Message); }
        }

        private static string Normalize(string t)
        {
            if (string.IsNullOrEmpty(t)) return "";
            var r = Regex.Replace(t, @"[\dA-Fa-f]{8,}|0x[0-9A-Fa-f]+|\d{10,}", "...");
            return r.Length > 60 ? r.Substring(0, 60) : r;
        }

        private static string Truncate(string t)
        {
            if (string.IsNullOrEmpty(t)) return "";
            t = t.Replace('\n', ' ').Replace('\r', ' ');
            return t.Length > 80 ? t.Substring(0, 77) + "..." : t;
        }

        public static string GetLogFolderPath()
        {
            try
            {
                var p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low",
                    "Ludeon Studios", "RimWorld by Ludeon Studios");
                if (Directory.Exists(p)) return p;
                return "";
            }
            catch { return ""; }
        }
    }
}