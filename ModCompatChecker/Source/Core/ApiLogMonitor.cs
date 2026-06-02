using System;
using System.Collections.Generic;
using System.Threading;

namespace ModCompatChecker.Core
{
    public class ApiLogEntry
    {
        public DateTime Timestamp;
        public string OperationType;  // "TestConnection", "AIDirSearch", "FollowUp", "CompatibilityScan", etc.
        public string Status;         // "Running", "Completed", "Failed", "Cancelled"
        public string Detail;         // Brief result or error message
        public int TokenEstimate;     // Estimated tokens used
    }

    public static class ApiLogMonitor
    {
        private static readonly List<ApiLogEntry> _entries = new List<ApiLogEntry>();
        private static readonly object _lock = new object();
        private const int MaxEntries = 40;

        /// <summary>Global cancel flag — when set, all API calls should abort.</summary>
        public static bool GlobalCancel;

        /// <summary>Block ALL subsequent API calls from this mod. Persistent until user unchecks.</summary>
        public static bool ApiBlocked;

        /// <summary>Thread-safe snapshot of entries for UI rendering.</summary>
        public static List<ApiLogEntry> GetEntries()
        {
            lock (_lock) { return new List<ApiLogEntry>(_entries); }
        }

        /// <summary>Number of currently running operations.</summary>
        public static int RunningCount
        {
            get { lock (_lock) { int c = 0; foreach (var e in _entries) if (e.Status == "Running") c++; return c; } }
        }

        /// <summary>Start logging an API operation. Returns the entry for later update.</summary>
        public static ApiLogEntry LogStart(string operationType)
        {
            var entry = new ApiLogEntry
            {
                Timestamp = DateTime.Now,
                OperationType = operationType,
                Status = "Running",
                Detail = ""
            };
            lock (_lock)
            {
                _entries.Add(entry);
                if (_entries.Count > MaxEntries)
                    _entries.RemoveRange(0, _entries.Count - MaxEntries);
            }
            return entry;
        }

        /// <summary>Mark an operation as completed.</summary>
        public static void LogComplete(ApiLogEntry entry, string detail, int tokenEstimate = 0)
        {
            lock (_lock)
            {
                entry.Status = "Completed";
                entry.Detail = Truncate(detail, 120);
                entry.TokenEstimate = tokenEstimate;
            }
        }

        /// <summary>Mark an operation as failed.</summary>
        public static void LogFailed(ApiLogEntry entry, string error)
        {
            lock (_lock)
            {
                entry.Status = "Failed";
                entry.Detail = Truncate(error, 120);
            }
        }

        /// <summary>Mark an operation as cancelled.</summary>
        public static void LogCancelled(ApiLogEntry entry)
        {
            lock (_lock)
            {
                entry.Status = "Cancelled";
                entry.Detail = "User cancelled";
            }
        }

        /// <summary>Force-stop all running API calls and clear the global cancel flag.</summary>
        public static void ForceStopAll()
        {
            GlobalCancel = true;
            lock (_lock)
            {
                foreach (var e in _entries)
                {
                    if (e.Status == "Running")
                    {
                        e.Status = "Cancelled";
                        e.Detail = "Force stopped";
                    }
                }
            }
            // Reset after a short delay so future calls can proceed
            new Thread(() => { Thread.Sleep(500); GlobalCancel = false; }) { IsBackground = true }.Start();
        }

        /// <summary>Toggle blocking of all subsequent API calls from this mod.</summary>
        public static void SetApiBlocked(bool blocked)
        {
            ApiBlocked = blocked;
            if (blocked) ForceStopAll();
        }

        /// <summary>Clear all log entries.</summary>
        public static void ClearLog()
        {
            lock (_lock) { _entries.Clear(); }
        }

        /// <summary>Reset on game lifecycle change (new game, load save, return to menu).</summary>
        public static void Reset()
        {
            lock (_lock) { _entries.Clear(); }
            ApiBlocked = false;
            GlobalCancel = false;
        }

        private static string Truncate(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (text.Length <= maxLen) return text;
            return text.Substring(0, maxLen) + "...";
        }
    }
}
