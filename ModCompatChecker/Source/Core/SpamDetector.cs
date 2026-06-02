using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using RimWorld;
using Verse;

namespace ModCompatChecker.Core
{
    public class SpamAlert
    {
        public string NormalizedMessage;
        public int Count;
        public bool IsActive;
        public float LastAlertTime;
    }

    public static class SpamDetector
    {
        private static readonly Dictionary<string, SpamAlert> _alerts = new Dictionary<string, SpamAlert>();
        private static float _lastAutoCheck;
        private const float AutoInterval = 60f;
        private const int SampleSize = 100;
        private const int SpamThreshold = 50;
        private const float AlertCooldown = 120f; // Same alert won't fire again within 2 min

        public static bool AutoDetectEnabled = false;
        public static readonly List<SpamAlert> ActiveAlerts = new List<SpamAlert>();

        public static void Reset()
        {
            _alerts.Clear();
            ActiveAlerts.Clear();
            _lastAutoCheck = 0f;
        }

        /// <summary>
        /// Call every frame from a mod component. Auto-checks every 60s.
        /// </summary>
        public static void Tick()
        {
            if (!AutoDetectEnabled) return;
            var now = Time.realtimeSinceStartup;
            if (now - _lastAutoCheck < AutoInterval) return;
            _lastAutoCheck = now;
            CheckForSpam();
        }

        /// <summary>
        /// Manual or auto check: scans last ~100 Error-level messages,
        /// normalizes, counts duplicates. Threshold: 50 in the batch.
        /// </summary>
        public static void CheckForSpam()
        {
            try
            {
                var messages = Log.Messages;
                if (messages == null) return;
                var now = Time.realtimeSinceStartup;

                // Take last ~100 errors only
                var recent = new List<string>();
                int total = 0;
                foreach (var msg in messages)
                {
                    total++;
                    var typeStr = msg.type.ToString();
                    if (!typeStr.Contains("Error")) continue;
                    recent.Add(msg.text ?? "");
                }

                // Only sample last SampleSize
                int start = Math.Max(0, recent.Count - SampleSize);
                var counts = new Dictionary<string, int>();
                var samples = new Dictionary<string, string>();

                for (int i = start; i < recent.Count; i++)
                {
                    var norm = Normalize(recent[i]);
                    if (!counts.ContainsKey(norm)) { counts[norm] = 0; samples[norm] = Truncate(recent[i]); }
                    counts[norm]++;
                }

                ActiveAlerts.Clear();
                foreach (var kv in counts)
                {
                    if (kv.Value >= SpamThreshold)
                    {
                        if (!_alerts.ContainsKey(kv.Key))
                            _alerts[kv.Key] = new SpamAlert();
                        var a = _alerts[kv.Key];
                        a.Count = kv.Value;
                        a.NormalizedMessage = samples[kv.Key] ?? kv.Key;
                        a.IsActive = true;

                        // Popup alert (with cooldown)
                        if (now - a.LastAlertTime > AlertCooldown)
                        {
                            a.LastAlertTime = now;
                            Messages.Message(
                                "ModCompatChecker.SpamAlert".Translate() + "\n[" + kv.Value + "x] " + a.NormalizedMessage,
                                MessageTypeDefOf.NegativeEvent);
                        }
                        ActiveAlerts.Add(a);
                    }
                }
            }
            catch (Exception ex) { Log.Warning("[ModCompatChecker] SpamDetector: " + ex.Message); }
        }

        private static string Normalize(string t)
        {
            if (string.IsNullOrEmpty(t)) return "";
            var r = Regex.Replace(t, @"[\dA-Fa-f]{8,}|0x[0-9A-Fa-f]+|\d{10,}|at .*:line \d+|\(at <[^>]+>\)", "...");
            return r.Length > 80 ? r.Substring(0, 80) : r;
        }

        private static string Truncate(string t)
        {
            if (string.IsNullOrEmpty(t)) return "";
            t = t.Replace('\n', ' ').Replace('\r', ' ');
            return t.Length > 100 ? t.Substring(0, 97) + "..." : t;
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