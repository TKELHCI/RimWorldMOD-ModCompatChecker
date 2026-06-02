using System.Collections.Generic;
using System.Text;
using ModCompatChecker.Core;
using Verse;

namespace ModCompatChecker.AI
{
    /// <summary>
    /// 构建发送给 AI 的提示词 — 所有输出格式统一为结构化卡片
    /// </summary>
    public static class PromptBuilder
    {
        private const string FormatZh = @"
请用以下卡片格式回答（简洁，200字以内）：

━━━━━━━━━━━━━━━━━━━━
涉及 MOD:
  • 模组名 (WorkshopID)

诊断:
  具体问题与影响（1-2句）

建议:
  • 操作建议（加载顺序/修复/兼容补丁）
━━━━━━━━━━━━━━━━━━━━";

        private const string FormatEn = @"
Reply in this card format (concise, under 200 words):

━━━━━━━━━━━━━━━━━━━━
MODs Involved:
  • ModName (WorkshopID)

Diagnosis:
  Specific issue and impact (1-2 sentences)

Suggestions:
  • Actionable advice (load order / fix / compat patch)
━━━━━━━━━━━━━━━━━━━━";

        private const string ErrFormatZh = @"
请用以下卡片格式回答（简洁，200字以内）：

━━━━━━━━━━━━━━━━━━━━
崩溃原因:
  (一句话总结)

涉及 MOD:
  • 模组名 (WorkshopID) — 角色

诊断:
  具体原因分析

修复建议:
  • 操作建议
━━━━━━━━━━━━━━━━━━━━";

        private const string ErrFormatEn = @"
Reply in this card format (concise, under 200 words):

━━━━━━━━━━━━━━━━━━━━
Crash Cause:
  (one sentence)

MODs Involved:
  • ModName (WorkshopID) — role

Diagnosis:
  Specific analysis

Fix Suggestions:
  • Actionable advice
━━━━━━━━━━━━━━━━━━━━";

        public static string BuildHarmonyConflictPrompt(HarmonyConflict conflict, string language)
        {
            var sb = new StringBuilder();
            if (language == "zh")
            {
                sb.AppendLine(GetSystemPrompt());
                sb.AppendLine("Harmony 补丁冲突分析：");
                sb.AppendLine();
                sb.AppendLine($"MOD A: {conflict.ModNameA} ({conflict.ModPackageIdA})");
                sb.AppendLine($"MOD B: {conflict.ModNameB} ({conflict.ModPackageIdB})");
                sb.AppendLine($"目标: {conflict.TargetType}.{conflict.TargetMethod}");
                sb.AppendLine($"补丁类型: A={conflict.PatchTypeA}  B={conflict.PatchTypeB}");
                sb.AppendLine($"风险: {conflict.Risk}");
                sb.AppendLine(FormatZh);
            }
            else
            {
                sb.AppendLine(GetSystemPrompt());
                sb.AppendLine("Harmony patch conflict analysis:");
                sb.AppendLine();
                sb.AppendLine($"Mod A: {conflict.ModNameA} ({conflict.ModPackageIdA})");
                sb.AppendLine($"Mod B: {conflict.ModNameB} ({conflict.ModPackageIdB})");
                sb.AppendLine($"Target: {conflict.TargetType}.{conflict.TargetMethod}");
                sb.AppendLine($"Patch types: A={conflict.PatchTypeA}  B={conflict.PatchTypeB}");
                sb.AppendLine($"Risk: {conflict.Risk}");
                sb.AppendLine(FormatEn);
            }
            return sb.ToString();
        }

        public static string BuildDefConflictPrompt(DefConflict conflict, string language)
        {
            var sb = new StringBuilder();
            if (language == "zh")
            {
                sb.AppendLine(GetSystemPrompt());
                sb.AppendLine("Def 覆盖冲突分析：");
                sb.AppendLine();
                sb.AppendLine($"MOD A: {conflict.ModNameA} ({conflict.ModPackageIdA})");
                sb.AppendLine($"MOD B: {conflict.ModNameB} ({conflict.ModPackageIdB})");
                sb.AppendLine($"目标 Def: {conflict.DefType}/{conflict.DefName}");
                sb.AppendLine($"XPath A: {conflict.XPathA}");
                sb.AppendLine($"XPath B: {conflict.XPathB}");
                sb.AppendLine(FormatZh);
            }
            else
            {
                sb.AppendLine(GetSystemPrompt());
                sb.AppendLine("Def override conflict analysis:");
                sb.AppendLine();
                sb.AppendLine($"Mod A: {conflict.ModNameA} ({conflict.ModPackageIdA})");
                sb.AppendLine($"Mod B: {conflict.ModNameB} ({conflict.ModPackageIdB})");
                sb.AppendLine($"Target Def: {conflict.DefType}/{conflict.DefName}");
                sb.AppendLine($"XPath A: {conflict.XPathA}");
                sb.AppendLine($"XPath B: {conflict.XPathB}");
                sb.AppendLine(FormatEn);
            }
            return sb.ToString();
        }

        public static string BuildErrorAnalysisPrompt(string errorStack, ConflictReport report, string language)
        {
            var sb = new StringBuilder();
            if (language == "zh")
            {
                sb.AppendLine(GetSystemPrompt());
                sb.AppendLine("崩溃日志分析：");
                sb.AppendLine();
                sb.AppendLine("=== 崩溃日志 ===");
                sb.AppendLine(errorStack);
                sb.AppendLine();
                sb.AppendLine("=== 已检测到的 MOD 冲突 ===");
                sb.AppendLine($"Harmony 冲突: {report.HarmonyConflicts.Count} 个");
                sb.AppendLine($"Def 冲突: {report.DefConflicts.Count} 个");
                sb.AppendLine($"依赖问题: {report.DependencyIssues.Count} 个");
                sb.AppendLine(ErrFormatZh);
            }
            else
            {
                sb.AppendLine(GetSystemPrompt());
                sb.AppendLine("Crash log analysis:");
                sb.AppendLine();
                sb.AppendLine("=== Crash Log ===");
                sb.AppendLine(errorStack);
                sb.AppendLine();
                sb.AppendLine("=== Detected Conflicts ===");
                sb.AppendLine($"Harmony: {report.HarmonyConflicts.Count}");
                sb.AppendLine($"Def: {report.DefConflicts.Count}");
                sb.AppendLine($"Dependency: {report.DependencyIssues.Count}");
                sb.AppendLine(ErrFormatEn);
            }
            return sb.ToString();
        }

        public static string BuildDependencyIssuePrompt(DependencyIssue issue, string language)
        {
            var sb = new StringBuilder();
            if (language == "zh")
            {
                sb.AppendLine(GetSystemPrompt());
                sb.AppendLine("依赖问题分析：");
                sb.AppendLine();
                sb.AppendLine($"MOD: {issue.ModName ?? "未知"}");
                sb.AppendLine($"PackageId: {issue.ModPackageId ?? "未知"}");
                sb.AppendLine($"相关依赖: {issue.RelatedPackageId ?? "未知"}");
                sb.AppendLine($"类型: {issue.Type}  风险: {issue.Risk}");
                sb.AppendLine($"详情: {issue.ExtraInfo ?? issue.Summary ?? "无"}");
                sb.AppendLine(FormatZh);
            }
            else
            {
                sb.AppendLine(GetSystemPrompt());
                sb.AppendLine("Dependency issue analysis:");
                sb.AppendLine();
                sb.AppendLine($"Mod: {issue.ModName ?? "Unknown"}");
                sb.AppendLine($"PackageId: {issue.ModPackageId ?? "Unknown"}");
                sb.AppendLine($"Related: {issue.RelatedPackageId ?? "Unknown"}");
                sb.AppendLine($"Type: {issue.Type}  Risk: {issue.Risk}");
                sb.AppendLine($"Details: {issue.ExtraInfo ?? issue.Summary ?? "None"}");
                sb.AppendLine(FormatEn);
            }
            return sb.ToString();
        }

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
