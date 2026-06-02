using System.Collections.Generic;
using System.Text;
using ModCompatChecker.Core;
using Verse;

namespace ModCompatChecker.AI
{
    /// <summary>
    /// 构建发送给 AI 的提示词
    /// </summary>
    public static class PromptBuilder
    {
        /// <summary>
        /// 为单个 Harmony 冲突构建分析 Prompt
        /// </summary>
        public static string BuildHarmonyConflictPrompt(HarmonyConflict conflict, string language)
        {
            var sb = new StringBuilder();
            if (language == "zh")
            {
                sb.AppendLine("你是 RimWorld MOD 兼容性分析专家。请分析以下 Harmony 补丁冲突：");
                sb.AppendLine();
                sb.AppendLine($"MOD A: {conflict.ModNameA} ({conflict.ModPackageIdA})");
                sb.AppendLine($"MOD B: {conflict.ModNameB} ({conflict.ModPackageIdB})");
                sb.AppendLine($"目标方法: {conflict.TargetType}.{conflict.TargetMethod}");
                sb.AppendLine($"MOD A 使用: {conflict.PatchTypeA}");
                sb.AppendLine($"MOD B 使用: {conflict.PatchTypeB}");
                sb.AppendLine($"风险等级: {conflict.Risk}");
                sb.AppendLine();
                sb.AppendLine("请简要分析：");
                sb.AppendLine("1. 这个冲突具体会导致什么问题？");
                sb.AppendLine("2. 哪个 MOD 应该先加载？");
                sb.AppendLine("3. 有没有已知的兼容补丁或解决方案？");
                sb.AppendLine("用中文回答，简洁明了，控制在 150 字以内。");
            }
            else
            {
                sb.AppendLine("You are a RimWorld mod compatibility expert. Analyze this Harmony patch conflict:");
                sb.AppendLine();
                sb.AppendLine($"Mod A: {conflict.ModNameA} ({conflict.ModPackageIdA})");
                sb.AppendLine($"Mod B: {conflict.ModNameB} ({conflict.ModPackageIdB})");
                sb.AppendLine($"Target: {conflict.TargetType}.{conflict.TargetMethod}");
                sb.AppendLine($"Patch A type: {conflict.PatchTypeA}");
                sb.AppendLine($"Patch B type: {conflict.PatchTypeB}");
                sb.AppendLine($"Risk: {conflict.Risk}");
                sb.AppendLine();
                sb.AppendLine("Analyze:");
                sb.AppendLine("1. What problem might this cause?");
                sb.AppendLine("2. Which mod should load first?");
                sb.AppendLine("3. Any known fix?");
                sb.AppendLine("Keep it concise, under 150 words.");
            }
            return sb.ToString();
        }

        /// <summary>
        /// 为单个 Def 冲突构建分析 Prompt
        /// </summary>
        public static string BuildDefConflictPrompt(DefConflict conflict, string language)
        {
            var sb = new StringBuilder();
            if (language == "zh")
            {
                sb.AppendLine("你是 RimWorld MOD 兼容性分析专家。请分析以下 Def 覆盖冲突：");
                sb.AppendLine();
                sb.AppendLine($"MOD A: {conflict.ModNameA} ({conflict.ModPackageIdA})");
                sb.AppendLine($"MOD B: {conflict.ModNameB} ({conflict.ModPackageIdB})");
                sb.AppendLine($"目标 Def: {conflict.DefType}/{conflict.DefName}");
                sb.AppendLine($"MOD A xpath: {conflict.XPathA}");
                sb.AppendLine($"MOD B xpath: {conflict.XPathB}");
                sb.AppendLine();
                sb.AppendLine("请简要分析这个冲突的影响和可能的解决方案。用中文回答，控制在 100 字以内。");
            }
            else
            {
                sb.AppendLine("You are a RimWorld mod compatibility expert. Analyze this Def override conflict:");
                sb.AppendLine();
                sb.AppendLine($"Mod A: {conflict.ModNameA} ({conflict.ModPackageIdA})");
                sb.AppendLine($"Mod B: {conflict.ModNameB} ({conflict.ModPackageIdB})");
                sb.AppendLine($"Target Def: {conflict.DefType}/{conflict.DefName}");
                sb.AppendLine($"XPath A: {conflict.XPathA}");
                sb.AppendLine($"XPath B: {conflict.XPathB}");
                sb.AppendLine();
                sb.AppendLine("Briefly analyze impact and possible fix. Under 100 words.");
            }
            return sb.ToString();
        }

        /// <summary>
        /// 为错误日志构建分析 Prompt
        /// </summary>
        public static string BuildErrorAnalysisPrompt(string errorStack, ConflictReport report, string language)
        {
            var sb = new StringBuilder();
            if (language == "zh")
            {
                sb.AppendLine("你是 RimWorld MOD 兼容性分析专家。请分析以下崩溃日志，并结合已检测到的 MOD 冲突给出诊断：");
                sb.AppendLine();
                sb.AppendLine("=== 崩溃日志 ===");
                sb.AppendLine(errorStack);
                sb.AppendLine();
                sb.AppendLine("=== 已检测到的 MOD 冲突 ===");
                sb.AppendLine($"Harmony 冲突: {report.HarmonyConflicts.Count} 个");
                sb.AppendLine($"Def 冲突: {report.DefConflicts.Count} 个");
                sb.AppendLine($"依赖问题: {report.DependencyIssues.Count} 个");
                sb.AppendLine();
                sb.AppendLine("请：1) 诊断崩溃原因；2) 指出最可能相关的 MOD；3) 给出修复建议。用中文，简洁明了。");
            }
            else
            {
                sb.AppendLine("RimWorld mod compatibility expert. Diagnose this crash with conflict data:");
                sb.AppendLine();
                sb.AppendLine("=== Crash Log ===");
                sb.AppendLine(errorStack);
                sb.AppendLine();
                sb.AppendLine("=== Detected Conflicts ===");
                sb.AppendLine($"Harmony: {report.HarmonyConflicts.Count}");
                sb.AppendLine($"Def: {report.DefConflicts.Count}");
                sb.AppendLine($"Dependency: {report.DependencyIssues.Count}");
                sb.AppendLine();
                sb.AppendLine("Diagnose, identify mods, suggest fix. Concise.");
            }
            return sb.ToString();
        }

        /// <summary>
        /// 为依赖问题构建分析 Prompt
        /// </summary>
        public static string BuildDependencyIssuePrompt(DependencyIssue issue, string language)
        {
            var sb = new StringBuilder();
            if (language == "zh")
            {
                sb.AppendLine("你是 RimWorld MOD 兼容性分析专家。请分析以下依赖问题并给出建议：");
                sb.AppendLine();
                sb.AppendLine("=== 依赖问题 ===");
                sb.AppendLine($"MOD: {issue.ModName ?? "未知"}");
                sb.AppendLine($"PackageId: {issue.ModPackageId ?? "未知"}");
                sb.AppendLine($"相关依赖: {issue.RelatedPackageId ?? "未知"}");
                sb.AppendLine($"问题类型: {issue.Type}");
                sb.AppendLine($"风险等级: {issue.Risk}");
                sb.AppendLine($"详情: {issue.ExtraInfo ?? issue.Summary ?? "无"}");
                sb.AppendLine();
                sb.AppendLine("请：1) 解释此依赖问题的含义；2) 评估对游戏的影响；3) 给出解决建议（包括可能需要安装/卸载的 MOD）。用中文，简洁明了。");
            }
            else
            {
                sb.AppendLine("RimWorld mod dependency expert. Analyze this dependency issue:");
                sb.AppendLine();
                sb.AppendLine("=== Dependency Issue ===");
                sb.AppendLine($"Mod: {issue.ModName ?? "Unknown"}");
                sb.AppendLine($"PackageId: {issue.ModPackageId ?? "Unknown"}");
                sb.AppendLine($"Related: {issue.RelatedPackageId ?? "Unknown"}");
                sb.AppendLine($"Type: {issue.Type}");
                sb.AppendLine($"Risk: {issue.Risk}");
                sb.AppendLine($"Details: {issue.ExtraInfo ?? issue.Summary ?? "None"}");
                sb.AppendLine();
                sb.AppendLine("Explain, assess impact, suggest resolution. Concise.");
            }
            return sb.ToString();
        }

        /// <summary>
        /// 检测游戏语言并返回提示词语言代码 (zh/en)
        /// </summary>
                /// <summary>
        /// 获取当前生效的系统预设指令（优先使用用户自定义，否则使用默认）
        /// </summary>
        public static string GetSystemPrompt()
        {
            var settings = ModCompatChecker.ModCompatMod.Instance?.Settings;
            if (settings != null && settings.UseCustomSystemPrompt && !string.IsNullOrEmpty(settings.CustomSystemPrompt))
                return settings.CustomSystemPrompt;
            var lang = GetPromptLanguage();
            return lang == "zh" ? ModCompatSettings.DefaultSystemPromptZh : ModCompatSettings.DefaultSystemPromptEn;
        }

        public static string GetPromptLanguage()
        {
            try
            {
                var lang = LanguageDatabase.activeLanguage?.FriendlyNameEnglish ?? "";
                if (lang.Contains("Chinese") || lang.Contains("Simplified") || lang.Contains("Traditional"))
                    return "zh";
                return "en";
            }
            catch { return "en"; }
        }
    }
}

