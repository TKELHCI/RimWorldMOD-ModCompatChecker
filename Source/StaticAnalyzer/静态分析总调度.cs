using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Verse;

namespace ModCompatChecker.StaticAnalyzer
{
    /// <summary>
    /// 静态分析总调度：协调所有子检查器，收集结果
    /// 比喻：总工头，挨个叫检查员去干活，最后汇总报告
    /// </summary>
    public static class 静态分析总调度
    {
        public enum 问题严重度 { 警告, 危险, 致命 }

        public class 单个发现
        {
            public string 来源;       // 检查器名
            public 问题严重度 严重度;
            public string Mod名;
            public string 描述;
            public string 位置;       // 文件路径
        }

        public class 扫描报告
        {
            public List<单个发现> 所有发现 = new List<单个发现>();
            public int 扫描Mod数;
            public long 耗时毫秒;
            public int Def完整性问题数;
            public int 贴图音频问题数;
            public int Harmony冲突数;
        }

        /// <summary>
        /// 主入口：跑全部静态分析
        /// </summary>
        public static 扫描报告 跑全部()
        {
            var 报告 = new 扫描报告();
            var 秒表 = Stopwatch.StartNew();

            var 所有Mods = LoadedModManager.RunningModsListForReading;
            var mods = 所有Mods
                .Where(m => m.PackageId != null && !m.PackageId.StartsWith("ludeon.", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (mods == null || mods.Count == 0)
            {
                Log.Warning("[ModCompatChecker] 静态分析：无已加载mod");
                return 报告;
            }

            报告.扫描Mod数 = mods.Count; // 玩家mod数

            // —— 1. Def完整性 ——
            try
            {
                var def问题们 = Def完整性检查器.检查(所有Mods);
                报告.Def完整性问题数 = def问题们.Count;
                foreach (var q in def问题们)
                {
                    报告.所有发现.Add(new 单个发现
                    {
                        来源 = "Def完整性",
                        严重度 = q.是致命 ? 问题严重度.致命 : 问题严重度.危险,
                        Mod名 = q.Mod名,
                        描述 = q.描述,
                        位置 = q.位置
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[ModCompatChecker] Def完整性检查失败: {ex}");
            }

            // —— 2. 贴图/音频预检 ——
            try
            {
                var 资源问题们 = 贴图音频预检器.检查(mods);
                报告.贴图音频问题数 = 资源问题们.Count;
                foreach (var q in 资源问题们)
                {
                    报告.所有发现.Add(new 单个发现
                    {
                        来源 = "贴图/音频",
                        严重度 = 问题严重度.警告,
                        Mod名 = q.Mod名,
                        描述 = $"{q.类型}文件不存在: {q.引用路径}",
                        位置 = q.来源文件
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[ModCompatChecker] 贴图音频预检失败: {ex}");
            }

            // —— 3. Harmony冲突（复用现有分析器） ——
            try
            {
                var harmony冲突们 = Core.HarmonyAnalyzer.Analyze(mods);
                报告.Harmony冲突数 = harmony冲突们.Count;
                foreach (var h in harmony冲突们)
                {
                    报告.所有发现.Add(new 单个发现
                    {
                        来源 = "Harmony冲突",
                        严重度 = h.IsTranspilerConflict ? 问题严重度.危险 : 问题严重度.警告,
                        Mod名 = h.ModNameA,
                        描述 = $"与 {h.ModNameB} 同时修补 {h.TargetType}.{h.TargetMethod}",
                        位置 = $"{h.ModNameA} ↔ {h.ModNameB}"
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[ModCompatChecker] Harmony分析失败: {ex}");
            }

            秒表.Stop();
            报告.耗时毫秒 = 秒表.ElapsedMilliseconds;

            Log.Message($"[ModCompatChecker] 静态分析完成: {报告.扫描Mod数} mod, " +
                $"{报告.所有发现.Count} 个发现, {报告.耗时毫秒}ms");

            return 报告;
        }
    }
}