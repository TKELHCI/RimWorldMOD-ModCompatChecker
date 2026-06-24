using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ModCompatChecker.Core
{
    public static class DependencyExtractor
    {
        private static readonly Regex[] Patterns = new Regex[]
        {
            // "需要 MOD: XXX" / "missing: XXX"
            new Regex(@"(?:需要|缺少|缺失|前置|依赖|require|miss|depend)[^\n]{0,20}?[：:\s]*['`""]?([\w\s\.\-_\u4e00-\u9fff]{3,40})['`""]?(?:$|[,;.，；。\n)])",
                RegexOptions.IgnoreCase),
            // packageId: com.author.modname
            new Regex(@"\b([a-zA-Z0-9_]+(?:\.[a-zA-Z0-9_]+){2,4})\b"),
            // "XXX not loaded" / "XXX 未加载" / "XXX 未找到"
            new Regex(@"['`""]?([\w\s\.\-_\u4e00-\u9fff]{3,40})['`""]?\s*(?:not\s+(?:loaded|found|installed)|未加载|未找到|未安装|找不到|not\s+present)",
                RegexOptions.IgnoreCase),
            // packageId from XML: <packageId>xxx</packageId>
            new Regex(@"<packageId>([^<]+)</packageId>"),
            // modDependencies li content
            new Regex(@"(?:depends|依赖|requires?)[^\n]{0,30}?[：:\s]*['`""]?([\w\s\.\-_\u4e00-\u9fff]{3,50})['`""]?",
                RegexOptions.IgnoreCase),
        };

        public static List<string> Extract(string errorText)
        {
            if (string.IsNullOrEmpty(errorText)) return new List<string>();

            var results = new HashSet<string>();

            foreach (var pattern in Patterns)
            {
                foreach (Match m in pattern.Matches(errorText))
                {
                    var name = m.Groups[1].Value.Trim();
                    if (IsValidModName(name))
                        results.Add(name);
                }
            }

            var filtered = new List<string>();
            foreach (var r in results)
            {
                if (r.Length >= 3 && !IsCommonWord(r) && !r.StartsWith("<") && !IsFramework(r))
                    filtered.Add(r);
            }

            return filtered;
        }

        private static bool IsValidModName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            if (name.Length < 3 || name.Length > 80) return false;
            if (int.TryParse(name, out _)) return false;
            if (name.StartsWith("0x") || name.StartsWith("0X")) return false;
            return true;
        }

        private static readonly HashSet<string> FrameworkPrefixes = new HashSet<string>
        {
            "System.", "UnityEngine.", "Unity.", "Mono.", "Microsoft.", "netstandard",
            "mscorlib", "Verse.", "RimWorld.", "Ludeon.", "Assembly-CSharp"
        };

        private static bool IsCommonWord(string word)
        {
            var lower = word.ToLowerInvariant();
            var commonWords = new HashSet<string>
            {
                "error", "exception", "warning", "system", "method", "object", "reference",
                "null", "true", "false", "void", "string", "type", "the", "this", "that",
                "错误", "异常", "警告", "系统", "类型", "方法", "对象", "引用", "空",
                "file", "line", "path", "config", "data", "info", "debug", "trace", "log",
                "version", "assembly", "module", "namespace", "class", "interface"
            };
            return commonWords.Contains(lower);
        }

        private static bool IsFramework(string name)
        {
            foreach (var prefix in FrameworkPrefixes)
                if (name.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
