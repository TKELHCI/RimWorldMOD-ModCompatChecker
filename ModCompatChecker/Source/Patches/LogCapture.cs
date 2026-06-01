using System;
using System.Collections.Generic;
using Verse;

namespace ModCompatChecker.Patches
{
    [StaticConstructorOnStartup]
    public static class LogCapture
    {
        public enum LogLevel { Info, Warning, Error }

        public class LogEntry
        {
            public string Timestamp;
            public string Message;
            public LogLevel Level;
        }

        /// <summary>
        /// 获取运行时日志最近 N 条
        /// </summary>
        public static List<LogEntry> GetRecent(int maxCount, bool showInfo, bool showWarning, bool showError)
        {
            var results = new List<LogEntry>(maxCount);
            try
            {
                var messages = Log.Messages;
                if (messages == null) return results;

                // 遍历所有消息，保留最新的匹配条目
                var tempList = new List<LogEntry>();
                int idx = 0;
                foreach (var msg in messages)
                {
                    idx++;
                    var text = msg.text ?? "";
                    var level = ClassifyLevel(msg);

                    if (level == LogLevel.Info && !showInfo) continue;
                    if (level == LogLevel.Warning && !showWarning) continue;
                    if (level == LogLevel.Error && !showError) continue;

                    tempList.Add(new LogEntry
                    {
                        Message = text,
                        Level = level,
                        Timestamp = "#" + idx
                    });
                }

                // 返回最近的 maxCount 条
                int skip = Math.Max(0, tempList.Count - maxCount);
                for (int i = skip; i < tempList.Count; i++)
                    results.Add(tempList[i]);
            }
            catch (Exception ex)
            {
                Log.Warning("[ModCompatChecker] LogCapture.GetRecent failed: " + ex.Message);
            }
            return results;
        }

        private static LogLevel ClassifyLevel(LogMessage msg)
        {
            try
            {
                // RimWorld 1.6: 尝试枚举值
                var typeStr = msg.type.ToString();
                if (typeStr.Contains("Error")) return LogLevel.Error;
                if (typeStr.Contains("Warning")) return LogLevel.Warning;
                if (typeStr.Contains("Info") || typeStr.Contains("Message")) return LogLevel.Info;

                // 回退：尝试整型转换
                int typeVal = (int)msg.type;
                if (typeVal >= 2) return LogLevel.Error;
                if (typeVal >= 1) return LogLevel.Warning;
                return LogLevel.Info;
            }
            catch
            {
                var text = msg.text ?? "";
                if (string.IsNullOrEmpty(text)) return LogLevel.Info;
                var lower = text.ToLowerInvariant();
                if (lower.Contains("exception") || lower.Contains("error:")) return LogLevel.Error;
                if (lower.Contains("warning")) return LogLevel.Warning;
                return LogLevel.Info;
            }
        }
    }
}
