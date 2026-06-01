using System;
using System.Collections.Generic;
using ModCompatChecker.AI;
using ModCompatChecker.Core;
using UnityEngine;
using Verse;

namespace ModCompatChecker.UI
{
    public class MainWindow : Window
    {
        private enum Tab { Harmony, Def, Dependency }
        private Tab _currentTab = Tab.Harmony;

        private ConflictReport _report;
        private bool _hasScanned;
        private bool _isScanning;
        private Vector2 _scrollPos = Vector2.zero;
        private readonly Dictionary<int, string> _aiResults = new Dictionary<int, string>();
        private readonly HashSet<int> _pendingAnalysis = new HashSet<int>();
        private bool _isAnalyzing;
        private string _analyzeStatus = "";
        private readonly object _lock = new object();
        private bool _disposed = false;

        private static readonly Color TabActiveColor = new Color(0.3f, 0.6f, 0.3f);
        private static readonly Color TabInactiveColor = new Color(0.2f, 0.2f, 0.2f);
        private static readonly Color HeaderColor = new Color(0.6f, 0.85f, 0.6f);
        private static readonly Color RiskHighColor = Color.red;
        private static readonly Color RiskMediumColor = new Color(1f, 0.7f, 0.2f);
        private static readonly Color RiskLowColor = new Color(0.5f, 0.8f, 0.5f);

        public override Vector2 InitialSize => new Vector2(900f, 680f);

        public MainWindow()
        {
            doCloseX = true;
            doCloseButton = true;
            closeOnAccept = false;
            absorbInputAroundWindow = true;
            draggable = true;
            resizeable = true;
            layer = WindowLayer.Dialog;
        }

        public override void PreClose() { _disposed = true; base.PreClose(); }


        public override void DoWindowContents(Rect inRect)
        {
            float curY = 0f;
            float margin = 8f;

            var titleRect = new Rect(margin, curY, inRect.width - margin * 2, 30f);
            Widgets.DrawBoxSolid(titleRect, new Color(0.15f, 0.15f, 0.15f));
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(titleRect.x + 10f, titleRect.y + 4f, titleRect.width - 20f, 22f),
                "MOD 兼容性分析");
            Text.Font = GameFont.Small;
            curY += 36f;

            var actionRect = new Rect(margin, curY, inRect.width - margin * 2, 34f);
            float btnW = 120f;

            bool scanning;
            bool scanned;
            lock (_lock) { scanning = _isScanning; scanned = _hasScanned; }

            if (!scanning)
            {
                if (Widgets.ButtonText(new Rect(actionRect.x, actionRect.y, btnW, 30f),
                    scanned ? "ModCompatChecker.Rescan".Translate() : "ModCompatChecker.StartScan".Translate()))
                    StartScan();
            }
            else
            {
                Widgets.Label(new Rect(actionRect.x, actionRect.y + 5f, btnW, 24f), "后台扫描中...");
            }

            bool analyzing;
            string analyzeStatus;
            lock (_lock) { analyzing = _isAnalyzing; analyzeStatus = _analyzeStatus; }

            if (scanned)
            {
                ConflictReport rpt;
                lock (_lock) { rpt = _report; }

                if (rpt != null && rpt.HasConflicts)
                {
                    var hasAI = ModCompatChecker.ModCompatMod.Instance?.Settings?.IsAIConfigured() ?? false;
                    if (hasAI)
                    {
                        var batchRect = new Rect(actionRect.x + btnW + 8f, actionRect.y, 130f, 30f);
                        if (Widgets.ButtonText(batchRect, analyzing ? "分析中..." : "AI 批量分析"))
                            BatchAnalyze();
                        if (!string.IsNullOrEmpty(analyzeStatus))
                            Widgets.Label(new Rect(actionRect.x + btnW + 146f, actionRect.y + 5f,
                                actionRect.width - btnW - 160f, 24f), analyzeStatus);
                    }
                }
            }
            curY += 40f;

            if (!scanned)
            {
                var hintRect = new Rect(margin, curY, inRect.width - margin * 2, 80f);
                Widgets.Label(hintRect, "点击「开始扫描」检测已加载 MOD 的兼容性问题\n\n"
                    + "扫描类型：Harmony 补丁冲突 · Def 覆盖冲突 · 依赖 & 排序问题");
                return;
            }

            ConflictReport report;
            lock (_lock) { report = _report; }
            if (report == null) return;

            var statsRect = new Rect(margin, curY, inRect.width - margin * 2, 22f);
            GUI.color = HeaderColor;
            Widgets.Label(statsRect, $"扫描完成  ·  {report.TotalLoadedMods} 个 MOD  ·  "
                + $"共 {report.TotalConflictCount} 个潜在问题");
            GUI.color = Color.white;
            curY += 28f;

            var tabRect = new Rect(margin, curY, inRect.width - margin * 2, 32f);
            DrawTabs(tabRect, report);
            curY += 38f;

            var listRect = new Rect(margin, curY, inRect.width - margin * 2, inRect.height - curY - margin);
            DrawConflictList(listRect, report);
        }

        private void DrawTabs(Rect rect, ConflictReport report)
        {
            float tw = (rect.width - 8f) / 3f;
            float gap = 4f;
            DrawTabBtn(new Rect(rect.x, rect.y, tw, rect.height), "Harmony 补丁", report.HarmonyConflicts.Count, Tab.Harmony);
            DrawTabBtn(new Rect(rect.x + tw + gap, rect.y, tw, rect.height), "Def 覆盖", report.DefConflicts.Count, Tab.Def);
            DrawTabBtn(new Rect(rect.x + (tw + gap) * 2, rect.y, tw, rect.height), "依赖 & 排序", report.DependencyIssues.Count, Tab.Dependency);
        }

        private void DrawTabBtn(Rect rect, string label, int count, Tab tab)
        {
            bool active = _currentTab == tab;
            GUI.color = active ? TabActiveColor : TabInactiveColor;
            Widgets.DrawBoxSolid(rect, GUI.color);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, $"{label}  ({count})");
            Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonInvisible(rect)) _currentTab = tab;
        }

        private void DrawConflictList(Rect rect, ConflictReport report)
        {
            float contentH = GetContentHeight(report);
            var viewRect = new Rect(0f, 0f, rect.width - 20f, Mathf.Max(contentH, rect.height));
            Widgets.BeginScrollView(rect, ref _scrollPos, viewRect);
            var inner = new Listing_Standard();
            inner.Begin(new Rect(0f, 0f, viewRect.width, viewRect.height));

            switch (_currentTab)
            {
                case Tab.Harmony: DrawHarmonyConflicts(inner, report); break;
                case Tab.Def: DrawDefConflicts(inner, report); break;
                case Tab.Dependency: DrawDependencyIssues(inner, report); break;
            }

            inner.End();
            Widgets.EndScrollView();
        }

        private void DrawHarmonyConflicts(Listing_Standard listing, ConflictReport report)
        {
            if (report.HarmonyConflicts.Count == 0)
            { listing.Label("未发现 Harmony 补丁冲突 ✓", -1); return; }
            for (int i = 0; i < report.HarmonyConflicts.Count; i++)
            {
                var c = report.HarmonyConflicts[i];
                DrawConflictCard(listing, GetRiskColor(c.Risk), GetRiskLabel(c.Risk),
                    c.Summary ?? $"{c.ModNameA} vs {c.ModNameB}",
                    $"目标: {c.TargetType}.{c.TargetMethod}",
                    $"补丁: {c.ModNameA}[{c.PatchTypeA}]  ·  {c.ModNameB}[{c.PatchTypeB}]", i);
            }
        }

        private void DrawDefConflicts(Listing_Standard listing, ConflictReport report)
        {
            if (report.DefConflicts.Count == 0)
            { listing.Label("未发现 Def 覆盖冲突 ✓", -1); return; }
            for (int i = 0; i < report.DefConflicts.Count; i++)
            {
                var c = report.DefConflicts[i];
                DrawConflictCard(listing, GetRiskColor(c.Risk), GetRiskLabel(c.Risk),
                    c.Summary ?? $"{c.ModNameA} vs {c.ModNameB}",
                    $"Def: {c.DefType}/{c.DefName}",
                    $"xpath: {Truncate(c.XPathA, 60)}", i + 1000);
            }
        }

        private void DrawDependencyIssues(Listing_Standard listing, ConflictReport report)
        {
            if (report.DependencyIssues.Count == 0)
            { listing.Label("未发现依赖问题 ✓", -1); return; }
            for (int i = 0; i < report.DependencyIssues.Count; i++)
            {
                var iss = report.DependencyIssues[i];
                DrawConflictCard(listing, GetRiskColor(iss.Risk), GetRiskLabel(iss.Risk),
                    iss.Summary ?? iss.ExtraInfo ?? "未知问题",
                    iss.RelatedPackageId ?? "", $"类型: {iss.Type}  ·  {iss.ModName}", i + 2000);

                // 联网搜索按钮（仅依赖选项卡）
                var settings = ModCompatChecker.ModCompatMod.Instance?.Settings;
                if (settings != null && settings.IsAIConfigured() && !string.IsNullOrEmpty(iss.RelatedPackageId))
                {
                    var webBtnRect = listing.GetRect(26f);
                    if (Widgets.ButtonText(new Rect(webBtnRect.x + 152f, webBtnRect.y, 100f, 24f), "ModCompatChecker.WebSearch".Translate()))
                    {
                        var searchTerm = iss.RelatedPackageId ?? iss.ModName;
                        var url = "https://steamcommunity.com/workshop/browse/" +
                            "?appid=294100&searchtext=" + Uri.EscapeDataString(searchTerm);
                        try { System.Diagnostics.Process.Start(url); }
                        catch { try { Application.OpenURL(url); } catch { } }
                    }
                }
            }
        }

        private void DrawConflictCard(Listing_Standard listing, Color riskColor, string riskLabel,
            string title, string detail1, string detail2, int aiKey)
        {
            var origColor = GUI.color;
            GUI.color = riskColor;
            listing.Label($"▌ {riskLabel}  {title}", -1);
            GUI.color = origColor;
            if (!string.IsNullOrEmpty(detail1)) listing.Label($"     {detail1}", -1);
            if (!string.IsNullOrEmpty(detail2)) listing.Label($"     {detail2}", -1);

                var settings = ModCompatChecker.ModCompatMod.Instance?.Settings;
                if (settings != null && settings.IsAIConfigured())
                {
                    var btnRect = listing.GetRect(26f);
                    bool isPending;
                    lock (_lock) { isPending = _pendingAnalysis.Contains(aiKey); }
                    if (isPending)
                        Widgets.Label(new Rect(btnRect.x + 20f, btnRect.y + 2f, 120f, 20f), "分析中...");
                    else if (Widgets.ButtonText(new Rect(btnRect.x + 20f, btnRect.y, 120f, 24f), "AI 分析"))
                        AnalyzeSingle(aiKey);
            }

            string aiRes;
            lock (_lock) { _aiResults.TryGetValue(aiKey, out aiRes); }
            if (!string.IsNullOrEmpty(aiRes))
            {
                GUI.color = new Color(0.85f, 0.85f, 0.85f);
                listing.Label("     [AI] " + aiRes, -1);
                GUI.color = Color.white;
            }
            listing.Gap(10f);
        }
        private void StartScan()
        {
            lock (_lock)
            {
                if (_isScanning) return;
                _isScanning = true;
                _aiResults.Clear();
                _pendingAnalysis.Clear();
                _analyzeStatus = "";
            }

            var thread = new System.Threading.Thread(() =>
            {
                ConflictReport result = null;
                try { result = ConflictDetector.RunFullScan(); }
                catch (Exception ex) { Log.Error($"[ModCompatChecker] Scan error: {ex.Message}"); }

                lock (_lock)
                {
                    _report = result;
                    _hasScanned = true;
                    _isScanning = false;
                }
            }) { IsBackground = true };
            thread.Start();
        }

        private void AnalyzeSingle(int idx)
        {
            var settings = ModCompatChecker.ModCompatMod.Instance?.Settings;
            if (settings == null || !settings.IsAIConfigured()) return;

            var mi = settings.GetSelectedModelInfo();
            var ep = settings.GetEndpointAndProvider(mi);
            var mid = settings.IsCustomModel() ? settings.CustomModelId : mi.Id;

            ConflictReport capRpt;
            lock (_lock) { capRpt = _report; }
            if (capRpt == null) return;
            lock (_lock) { _pendingAnalysis.Add(idx); }

            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    string result;
                    if (idx >= 2000 && idx - 2000 < capRpt.DependencyIssues.Count)
                        result = AIService.AnalyzeDependencyIssue(capRpt.DependencyIssues[idx - 2000],
                            ep.endpoint, settings.APIKey, mid, ep.provider);
                    else if (idx < 1000 && idx < capRpt.HarmonyConflicts.Count)
                        result = AIService.AnalyzeHarmonyConflict(capRpt.HarmonyConflicts[idx],
                            ep.endpoint, settings.APIKey, mid, ep.provider);
                    else if (idx >= 1000 && idx < 2000 && idx - 1000 < capRpt.DefConflicts.Count)
                        result = AIService.AnalyzeDefConflict(capRpt.DefConflicts[idx - 1000],
                            ep.endpoint, settings.APIKey, mid, ep.provider);
                    else
                        result = "ModCompatChecker.InvalidRequest".Translate();

                    if (!_disposed) lock (_lock) { _aiResults[idx] = result; _pendingAnalysis.Remove(idx); }
                }
                catch (Exception ex)
                {
                    if (!_disposed) lock (_lock) { _aiResults[idx] = "AI 错误: " + ex.Message; _pendingAnalysis.Remove(idx); }
                }
            }) { IsBackground = true };
            thread.Start();
        }
        private void BatchAnalyze()
        {
            var settings = ModCompatChecker.ModCompatMod.Instance?.Settings;
            if (settings == null || !settings.IsAIConfigured()) return;

            lock (_lock)
            {
                if (_isAnalyzing) return;
                _isAnalyzing = true;
                _aiResults.Clear();
                _pendingAnalysis.Clear();
            }

            var mi = settings.GetSelectedModelInfo();
            var ep = settings.GetEndpointAndProvider(mi);
            var mid = settings.IsCustomModel() ? settings.CustomModelId : mi.Id;

            ConflictReport capRpt;
            lock (_lock) { capRpt = _report; }
            if (capRpt == null) { lock (_lock) { _isAnalyzing = false; } return; }

            var thread = new System.Threading.Thread(() =>
            {
                int total = capRpt.HarmonyConflicts.Count + capRpt.DefConflicts.Count + capRpt.DependencyIssues.Count;
                int done = 0;
                for (int i = 0; i < capRpt.HarmonyConflicts.Count; i++)
                {
                    if (_disposed) break;
                    try
                    {
                        var r = AIService.AnalyzeHarmonyConflict(capRpt.HarmonyConflicts[i],
                            ep.endpoint, settings.APIKey, mid, ep.provider);
                        if (!_disposed) lock (_lock) { _aiResults[i] = r; }
                    }
                    catch (Exception ex) { if (!_disposed) lock (_lock) { _aiResults[i] = "错误: " + ex.Message; } }
                    if (_disposed) break;
                    done++;
                    if (!_disposed) lock (_lock) { _analyzeStatus = $"分析中: {done}/{total}"; }
                }
                for (int i = 0; i < capRpt.DefConflicts.Count; i++)
                {
                    if (_disposed) break;
                    try
                    {
                        var r = AIService.AnalyzeDefConflict(capRpt.DefConflicts[i],
                            ep.endpoint, settings.APIKey, mid, ep.provider);
                        if (!_disposed) lock (_lock) { _aiResults[i + 1000] = r; }
                    }
                    catch (Exception ex) { if (!_disposed) lock (_lock) { _aiResults[i + 1000] = "错误: " + ex.Message; } }
                    if (_disposed) break;
                    done++;
                    if (!_disposed) lock (_lock) { _analyzeStatus = $"分析中: {done}/{total}"; }
                }
                for (int i = 0; i < capRpt.DependencyIssues.Count; i++)
                {
                    if (_disposed) break;
                    try
                    {
                        var r = AIService.AnalyzeDependencyIssue(capRpt.DependencyIssues[i],
                            ep.endpoint, settings.APIKey, mid, ep.provider);
                        if (!_disposed) lock (_lock) { _aiResults[i + 2000] = r; }
                    }
                    catch (Exception ex) { if (!_disposed) lock (_lock) { _aiResults[i + 2000] = "错误: " + ex.Message; } }
                    if (_disposed) break;
                    done++;
                    if (!_disposed) lock (_lock) { _analyzeStatus = $"分析中: {done}/{total}"; }
                }
                if (!_disposed) lock (_lock)
                {
                    _isAnalyzing = false;
                    _analyzeStatus = $"完成 ({total} 项)";
                }
            }) { IsBackground = true };
            thread.Start();
        }
        private float GetContentHeight(ConflictReport report)
        {
            float perItem = 110f;
            int count;
            switch (_currentTab)
            {
                case Tab.Harmony: count = report.HarmonyConflicts.Count; perItem = 110f; break;
                case Tab.Def: count = report.DefConflicts.Count; perItem = 100f; break;
                default: count = report.DependencyIssues.Count; perItem = 120f; break;
            }
            return Mathf.Max(count * perItem + 40f, 100f);
        }

        private static Color GetRiskColor(ConflictRisk risk)
        {
            switch (risk)
            {
                case ConflictRisk.High: return RiskHighColor;
                case ConflictRisk.Medium: return RiskMediumColor;
                default: return RiskLowColor;
            }
        }

        private static string GetRiskLabel(ConflictRisk risk)
        {
            switch (risk)
            {
                case ConflictRisk.High: return "⚠ 高风险";
                case ConflictRisk.Medium: return "● 中风险";
                default: return "○ 低风险";
            }
        }

        private static string Truncate(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLen) return text;
            return text.Substring(0, maxLen) + "...";
        }
    }
}
