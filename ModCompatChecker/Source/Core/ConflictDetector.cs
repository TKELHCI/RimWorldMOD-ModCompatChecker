using System;
using System.Collections.Generic;
using Verse;

namespace ModCompatChecker.Core
{
    /// <summary>
    /// 协调所有分析器，生成完整的冲突报告
    /// </summary>
    public static class ConflictDetector
    {
        /// <summary>
        /// 执行全量扫描
        /// </summary>
        public static ConflictReport RunFullScan()
        {
            var report = new ConflictReport();
            var mods = LoadedModManager.RunningModsListForReading;

            if (mods == null || mods.Count == 0)
            {
                Log.Warning("[ModCompatChecker] No mods loaded. Cannot perform scan.");
                return report;
            }

            report.TotalLoadedMods = mods.Count;

            try
            {
                // 1. Harmony 补丁冲突
                report.HarmonyConflicts = HarmonyAnalyzer.Analyze(mods);
            }
            catch (Exception ex)
            {
                Log.Error($"[ModCompatChecker] Harmony analysis failed: {ex}");
            }

            try
            {
                // 2. Def 覆盖冲突
                report.DefConflicts = DefAnalyzer.Analyze(mods);
            }
            catch (Exception ex)
            {
                Log.Error($"[ModCompatChecker] Def analysis failed: {ex}");
            }

            try
            {
                // 3. 依赖与排序
                report.DependencyIssues = DependencyChecker.Analyze(mods);
            }
            catch (Exception ex)
            {
                Log.Error($"[ModCompatChecker] Dependency check failed: {ex}");
            }

            Log.Message($"[ModCompatChecker] Full scan complete. " +
                        $"Harmony: {report.HarmonyConflicts.Count}, " +
                        $"Def: {report.DefConflicts.Count}, " +
                        $"Dependency: {report.DependencyIssues.Count}");

            return report;
        }
    }
}
