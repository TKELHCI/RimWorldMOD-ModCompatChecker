using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ModCompatChecker.AI;
using UnityEngine;
using Verse;

namespace ModCompatChecker.Core
{
    public class ErrorEntry
    {
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
        private static bool _initialized;

        public static List<ErrorEntry> Entries
        {
            get { if (!_initialized) Initialize(); return _entries; }
        }

        private static void Initialize()
        {
            _initialized = true;
            _entries = new List<ErrorEntry>
            {
                new ErrorEntry { Keyword = "NullReferenceException", Pattern = @"NullReferenceException", Category = "crash", Severity = "critical", ExplanationZh = "空引用异常 — 代码试图访问一个不存在的对象。最常见原因是 MOD 补丁目标方法不存在、或两个 MOD 互相覆盖导致对象被清空。通常会导致游戏崩溃或功能完全失效。", ExplanationEn = "Null reference exception — code tried to access an object that does not exist. Most commonly caused by a mod patch targeting a method that no longer exists, or two mods overwriting each other. Usually leads to crash or complete feature failure." },
                new ErrorEntry { Keyword = "StackOverflowException", Pattern = @"StackOverflowException", Category = "crash", Severity = "critical", ExplanationZh = "栈溢出 — 方法无限递归调用自身，通常是两个 MOD 的 Harmony 补丁互相触发导致的死循环。游戏会立即卡死或崩溃。", ExplanationEn = "Stack overflow — a method calls itself infinitely, usually caused by two Harmony patches triggering each other in a loop. Game will freeze or crash immediately." },
                new ErrorEntry { Keyword = "OutOfMemoryException", Pattern = @"OutOfMemoryException|Out of memory", Category = "crash", Severity = "critical", ExplanationZh = "内存不足 — 游戏或 MOD 占用了过多内存。可能是纹理太大、内存泄漏、或加载了过多 MOD。建议关闭大型纹理 MOD 或增加虚拟内存。", ExplanationEn = "Out of memory — the game or a mod consumed too much RAM. Possibly caused by oversized textures, memory leaks, or too many mods loaded. Try disabling large texture mods or increasing virtual memory." },
                new ErrorEntry { Keyword = "TypeLoadException", Pattern = @"TypeLoadException|TypeLoad", Category = "crash", Severity = "critical", ExplanationZh = "类型加载失败 — 某个 MOD 引用的类型在运行时不存在。通常是 MOD 版本不匹配、缺少依赖 DLL、或 MOD 未正确编译。", ExplanationEn = "Type load failure — a type referenced by a mod does not exist at runtime. Usually caused by version mismatch, missing dependency DLL, or incorrectly compiled mod." },
                new ErrorEntry { Keyword = "MissingMethodException", Pattern = @"MissingMethodException", Category = "crash", Severity = "critical", ExplanationZh = "方法缺失 — 代码调用了不存在的方法。通常是 MOD 针对的游戏版本不对、或 Harmony 补丁目标方法签名已变更。", ExplanationEn = "Missing method — code called a method that does not exist. Usually the mod targets a different game version, or the Harmony patch target method signature has changed." },
                new ErrorEntry { Keyword = "Could not resolve cross-reference", Pattern = @"Could not resolve cross-reference", Category = "crash", Severity = "high", ExplanationZh = "XML 交叉引用失败 — Def 文件中引用了不存在的对象。通常是 MOD 缺少前置依赖、或加载顺序错误导致 Def 尚未加载就被引用。", ExplanationEn = "XML cross-reference failed — a Def file references an object that does not exist. Usually the mod is missing a prerequisite, or load order is wrong." },
                new ErrorEntry { Keyword = "Harmony patch failed", Pattern = @"Harmony\s+\w+:\s*failed|Harmony\s+patch\s+failed", Category = "compatibility", Severity = "high", ExplanationZh = "Harmony 补丁失败 — MOD 的 Harmony 补丁未能成功应用。原因可能是目标方法已被其他 MOD 修改、方法签名变更、或补丁顺序冲突。建议用此 MOD 的 Harmony 补丁检测功能检查。", ExplanationEn = "Harmony patch failed — a mod Harmony patch could not be applied. May be because the target method was already modified by another mod. Use this mod Harmony detection to check." },
                new ErrorEntry { Keyword = "Could not load file or assembly", Pattern = @"Could not load file or assembly", Category = "crash", Severity = "critical", ExplanationZh = "DLL 加载失败 — 游戏无法加载 MOD 的 DLL 文件。可能是文件损坏、缺少 .NET 依赖、或 DLL 被安全软件拦截。", ExplanationEn = "DLL load failure — the game cannot load a mod DLL file. Possibly corrupted file, missing .NET dependency, or DLL blocked by antivirus." },
                new ErrorEntry { Keyword = "Tried to load duplicate", Pattern = @"Tried to load duplicate|already has a", Category = "warning", Severity = "medium", ExplanationZh = "重复加载 — 同一个 Def 或资源被多个 MOD 同时定义。游戏会使用后加载的那个，但可能导致不可预期的行为。", ExplanationEn = "Duplicate load — the same Def or resource is defined by multiple mods. The game uses the last-loaded version, possibly causing unexpected behavior." },
                new ErrorEntry { Keyword = "XmlException", Pattern = @"XmlException|Error parsing", Category = "warning", Severity = "medium", ExplanationZh = "XML 解析错误 — MOD 的 XML 文件格式有问题（如标签未闭合、特殊字符未转义）。常见于 & 未写成 &amp;。", ExplanationEn = "XML parse error — a mod XML file has formatting issues. Commonly caused by unescaped & (should be &amp;)." },
                new ErrorEntry { Keyword = "Translation error", Pattern = @"Translation\s+error|Keyed\s+not\s+found", Category = "warning", Severity = "low", ExplanationZh = "翻译键缺失 — MOD 的翻译文件中缺少某个文本键。不影响功能，但游戏内会显示原始键名。", ExplanationEn = "Missing translation key — a text key is missing from translation files. Does not affect functionality, but raw key names will display." },
                new ErrorEntry { Keyword = "Failed to find", Pattern = @"Failed to find\s+\w+", Category = "compatibility", Severity = "medium", ExplanationZh = "资源查找失败 — 游戏或 MOD 在运行时找不到某个资源。可能被其他 MOD 移除或改名导致。", ExplanationEn = "Resource lookup failed — game or mod cannot find a resource at runtime. May have been removed or renamed by another mod." },
                new ErrorEntry { Keyword = "Could not reserve", Pattern = @"Could not reserve|Could not find region", Category = "performance", Severity = "low", ExplanationZh = "区域/寻路错误 — 通常不影响游戏运行，但可能在某些情况下导致性能下降或寻路异常。", ExplanationEn = "Region/pathfinding error — usually does not affect gameplay, but may cause performance drops or pathfinding issues." },
                new ErrorEntry { Keyword = "Ticker", Pattern = @"Ticker|ticking\s+\w+\s+threw", Category = "performance", Severity = "medium", ExplanationZh = "Tick 异常 — 某个实体的每帧更新逻辑抛出了异常。单个报错通常无大碍，但大量报错会严重拖慢游戏速度。", ExplanationEn = "Tick exception — an entity per-frame update threw an exception. Occasionally harmless, but frequent errors can severely slow down the game." },
                new ErrorEntry { Keyword = "Assembly-CSharp", Pattern = @"Assembly-CSharp|ReflectionTypeLoadException", Category = "crash", Severity = "high", ExplanationZh = "程序集反射异常 — MOD 使用反射加载游戏类型时失败。通常是 MOD 版本与游戏版本不匹配。", ExplanationEn = "Assembly reflection exception — mod failed to load game types via reflection. Usually mod version does not match game version." }
            };
        }

        public static List<(ErrorEntry Entry, System.Text.RegularExpressions.Match Match)> MatchError(string errorText)
        {
            var results = new List<(ErrorEntry, System.Text.RegularExpressions.Match)>();
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
    }
}