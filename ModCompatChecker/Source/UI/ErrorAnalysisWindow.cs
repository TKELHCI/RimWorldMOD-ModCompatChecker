using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using ModCompatChecker.AI;
using ModCompatChecker.Core;
using ModCompatChecker.Patches;
using UnityEngine;
using Verse;

namespace ModCompatChecker.UI
{
    public class ErrorAnalysisWindow : Window
    {
        private enum ErrorSource { LogFile, Console, Clipboard }

        // 每个数据源独立的 AI 分析结果
        private class SourceResult
        {
            public string AiResult = "";
            public string CostInfo = "";
            public string FollowUpAnswer = "";
            public bool ShowFollowUp = false;
            public bool ShowSteamSearch = false;
            public List<string> DetectedDeps = new List<string>();
            public bool IsAnalyzing = false;
            public bool IsFollowUpAnalyzing = false;
            public bool CancelRequested = false;
            public Stopwatch Stopwatch = new Stopwatch();
        }
        private readonly Dictionary<ErrorSource, SourceResult> _results = new Dictionary<ErrorSource, SourceResult>
        {
            { ErrorSource.LogFile, new SourceResult() },
            { ErrorSource.Console, new SourceResult() },
            { ErrorSource.Clipboard, new SourceResult() },
        };
        private SourceResult Res => _results[_source];

        private ErrorSource _source = ErrorSource.Console;
        private List<ErrorItem> _errors = new List<ErrorItem>();
        private Vector2 _scrollPos = Vector2.zero;
        private Vector2 _resultScrollPos = Vector2.zero;
        private int _selectedCount = 1;
        private int _maxSelectable = 0;
        private string _clipboardText = "";
        private static int TimeoutSeconds = 20;
        private readonly object _lock = new object();
        private bool _disposed = false;
        private bool _needsRefresh = true;

        private string _followUpQuestion = "";

        private static readonly Regex TimestampRegex = new Regex(@"\[(\d{2}:\d{2}:\d{2})\]", RegexOptions.Compiled);
        private const int RimWorldAppId = 294100;

        // 运行时日志筛选
        private bool _showInfo = true;
        private bool _showWarning = true;
        private bool _showError = true;
        private int _quantityPreset = 10;
        private int _customQuantity = 30;
        private bool _showQuantitySelector = false;
        private static readonly int[] Presets = { 1, 5, 10, 20 };

        public override Vector2 InitialSize => new Vector2(760f, 640f);

        public override void PreClose() { _disposed = true; base.PreClose(); }

        public ErrorAnalysisWindow()
        {
            doCloseX = true;
            doCloseButton = true;
            closeOnAccept = false;
            absorbInputAroundWindow = true;
            draggable = true;
            resizeable = true;
            layer = WindowLayer.Dialog;
            TimeoutSeconds = ModCompatChecker.ModCompatMod.Instance?.Settings?.AnalysisTimeoutSeconds ?? 20;
            _needsRefresh = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (_needsRefresh) { _needsRefresh = false; RefreshErrorsNow(); }

            var listing = new Listing_Standard();
            listing.Begin(inRect);

            Text.Font = GameFont.Medium;
            listing.Label("ModCompatChecker.ErrorLogTitle".Translate(), -1);
            Text.Font = GameFont.Small;
            listing.Gap(2f);

            // 数据源选项卡
            var srcRect = listing.GetRect(26f);
            float tw = srcRect.width / 3f;
            DrawSourceTab(new Rect(srcRect.x, srcRect.y, tw, 24f), "ModCompatChecker.LogFile".Translate(), ErrorSource.LogFile);
            DrawSourceTab(new Rect(srcRect.x + tw, srcRect.y, tw, 24f), "ModCompatChecker.RuntimeLog".Translate(), ErrorSource.Console);
            DrawSourceTab(new Rect(srcRect.x + tw * 2, srcRect.y, tw, 24f), "ModCompatChecker.Clipboard".Translate(), ErrorSource.Clipboard);
            listing.Gap(4f);

            // 运行时日志筛选区
            if (_source == ErrorSource.Console)
            {
                DrawConsoleFilters(listing);
                listing.Gap(4f);
            }

            // 剪切板输入
            if (_source == ErrorSource.Clipboard)
            {
                var clipEntryRect = listing.GetRect(60f);
                _clipboardText = GUI.TextArea(clipEntryRect, _clipboardText ?? "", 200);
                listing.Gap(2f);
                if (listing.ButtonText("ModCompatChecker.ReadClipboard".Translate()))
                    _clipboardText = GUIUtility.systemCopyBuffer ?? "";
                listing.Gap(4f);
            }

            // 错误列表
            if (_errors.Count > 0 && _source != ErrorSource.Clipboard)
            {
                var listHeight = inRect.height * 0.22f;
                var listRect = listing.GetRect(listHeight);
                Widgets.BeginScrollView(listRect, ref _scrollPos,
                    new Rect(0f, 0f, listRect.width - 20f, _errors.Count * 22f));
                var inner = new Listing_Standard();
                inner.Begin(new Rect(0f, 0f, listRect.width - 20f, _errors.Count * 22f));
                for (int i = 0; i < _errors.Count; i++)
                {
                    var e = _errors[i];
                    GUI.color = e.Selected ? new Color(0.3f, 0.65f, 0.3f) : GetLevelColor(e.Level);
                    if (inner.ButtonText($"[{e.Level}] {e.Timestamp}  {Truncate(e.Summary, 70)}"))
                        e.Selected = !e.Selected;
                }
                GUI.color = Color.white;
                inner.End();
                Widgets.EndScrollView();
                listing.Gap(4f);

                if (_source != ErrorSource.Console)
                    DrawQuantitySelector(listing);
            }

            // 费用估算
            if (!string.IsNullOrEmpty(Res.CostInfo))
            {
                GUI.color = new Color(0.7f, 0.7f, 0.3f);
                listing.Label("ModCompatChecker.CostEstimate".Translate() + " " + Res.CostInfo, -1);
                GUI.color = Color.white;
            }

            // 分析按钮
            var settings = ModCompatChecker.ModCompatMod.Instance?.Settings;
            if (settings != null && settings.IsAIConfigured())
            {
                if (Res.IsAnalyzing)
                {
                    listing.Label("ModCompatChecker.AnalyzingSpace".Translate() + "ModCompatChecker.TimeCost".Translate() + Res.Stopwatch.Elapsed.TotalSeconds.ToString("F1") + "ModCompatChecker.SecondsLabel".Translate(), -1);
                    if (listing.ButtonText("ModCompatChecker.CancelAnalysis".Translate())) Res.CancelRequested = true;
                }
                else
                {
                    if (listing.ButtonText("ModCompatChecker.StartAIAnalysis".Translate()))
                        StartAnalyze();
                }
            }
            else
            {
                listing.Label("ModCompatChecker.PleaseConfigAIKey".Translate(), -1);
            }

            listing.Gap(4f);

            // AI 结果区（每个数据源独立）
            if (!string.IsNullOrEmpty(Res.AiResult))
            {
                // 复制按钮
                var copyRect = listing.GetRect(24f);
                if (Widgets.ButtonText(new Rect(copyRect.x + copyRect.width - 80f, copyRect.y, 80f, 22f), "ModCompatChecker.CopyResult".Translate()))
                {
                    GUIUtility.systemCopyBuffer = Res.AiResult;
                    // 复制成功（无需弹窗）
                }

                // 结果滚动区（自动换行）
                var resultRect = listing.GetRect(Mathf.Max(150f, inRect.height * 0.25f));
                Widgets.DrawBoxSolid(resultRect, new Color(0.1f, 0.1f, 0.1f, 0.8f));
                var innerRect = new Rect(resultRect.x + 6f, resultRect.y + 4f,
                    resultRect.width - 20f, resultRect.height - 8f);

                // 计算文本高度
                float textHeight = Text.CalcHeight(Res.AiResult, innerRect.width);
                var viewRect = new Rect(0f, 0f, innerRect.width - 4f, textHeight);
                Widgets.BeginScrollView(innerRect, ref _resultScrollPos, viewRect);
                GUI.color = new Color(0.85f, 0.85f, 0.85f);
                Widgets.Label(new Rect(0f, 0f, innerRect.width - 4f, textHeight), Res.AiResult);
                GUI.color = Color.white;
                Widgets.EndScrollView();
                listing.Gap(4f);
            }

            // 追问分析（可折叠）
            if (!string.IsNullOrEmpty(Res.AiResult))
            {
                ResultRenderer.DrawCollapsibleSection(listing, ref Res.ShowFollowUp, "ModCompatChecker.FollowUpAnalysis".Translate(), 120f, () =>
                {
                    var fqRect = listing.GetRect(50f);
                    _followUpQuestion = GUI.TextArea(new Rect(fqRect.x, fqRect.y, fqRect.width * 0.75f, 48f), _followUpQuestion ?? "");
                    listing.Gap(2f);
                    var btnRect = listing.GetRect(26f);
                    if (Res.IsFollowUpAnalyzing)
                        Widgets.Label(new Rect(btnRect.x, btnRect.y, 100f, 24f), "ModCompatChecker.Analyzing".Translate());
                    else if (Widgets.ButtonText(new Rect(btnRect.x, btnRect.y, 100f, 24f), "ModCompatChecker.SendFollowUp".Translate()))
                        DoFollowUp();
                    if (!string.IsNullOrEmpty(Res.FollowUpAnswer))
                    {
                        listing.Gap(4f);
                        var ansRect = listing.GetRect(60f);
                        Widgets.DrawBoxSolid(ansRect, new Color(0.08f, 0.12f, 0.08f));
                        float fupHeight = Text.CalcHeight(Res.FollowUpAnswer, ansRect.width - 8f);
                        Widgets.Label(new Rect(ansRect.x + 4f, ansRect.y + 2f, ansRect.width - 8f, fupHeight), Res.FollowUpAnswer);
                    }
                });
            }

            // Steam 搜索必需MOD（可折叠，使用AI关键词）
            if (!string.IsNullOrEmpty(Res.AiResult) && Res.DetectedDeps.Count > 0)
            {
                ResultRenderer.DrawCollapsibleSection(listing, ref Res.ShowSteamSearch,
                    "ModCompatChecker.SearchRequiredMod".Translate() + " (" + Res.DetectedDeps.Count + ")", Res.DetectedDeps.Count * 34f + 20f, () =>
                    {
                        listing.Label("ModCompatChecker.DetectedMissingMods".Translate(), -1);
                        foreach (var dep in Res.DetectedDeps)
                        {
                            var cardRect = listing.GetRect(28f);
                            Widgets.DrawBoxSolid(cardRect, new Color(0.12f, 0.18f, 0.28f));
                            // MOD标题显示在左侧
                            var title = dep.Length > 35 ? dep.Substring(0, 35) + "..." : dep;
                            Widgets.Label(new Rect(cardRect.x + 8f, cardRect.y + 4f, cardRect.width * 0.55f, 20f), "[MOD] " + title);
                            // 搜索字符串使用关键词
                            var searchKw = ExtractSearchKeyword(dep);
                            var btnRect = new Rect(cardRect.x + cardRect.width * 0.6f, cardRect.y + 2f, cardRect.width * 0.38f, 24f);
                            if (Widgets.ButtonText(btnRect, "ModCompatChecker.ViewOnSteam".Translate()))
                            {
                                var url = "https://steamcommunity.com/workshop/browse/" +
                                    "?appid=" + RimWorldAppId + "&searchtext=" + Uri.EscapeDataString(searchKw);
                                try { System.Diagnostics.Process.Start(url); }
                                catch { try { Application.OpenURL(url); } catch { } }
                            }
                        }
                    });
            }

            listing.End();
        }

        // 从检测到的依赖名中提取关键词用于Steam搜索
        private string ExtractSearchKeyword(string dep)
        {
            // 优先使用纯英文/数字部分或中文短词
            var match = Regex.Match(dep, @"^[\w\.\-]+$");
            if (match.Success) return dep;
            // 中文+英文混合：提取最长的有意义片段
            match = Regex.Match(dep, @"[\u4e00-\u9fff]{2,}|[a-zA-Z]{3,}");
            if (match.Success) return match.Value;
            return dep.Length > 30 ? dep.Substring(0, 30) : dep;
        }

        private void DrawConsoleFilters(Listing_Standard listing)
        {
            bool changed = false;

            // 级别筛选：三复选框并排
            var filterRect = listing.GetRect(24f);
            float cw = Math.Max(80f, (filterRect.width - 16f) / 3f);

            if (DrawCheckbox(filterRect.x, filterRect.y, cw, "Info", ref _showInfo, new Color(0.7f, 0.7f, 0.7f))) changed = true;
            if (DrawCheckbox(filterRect.x + cw, filterRect.y, cw, "Warning", ref _showWarning, new Color(0.9f, 0.75f, 0.3f))) changed = true;
            if (DrawCheckbox(filterRect.x + cw * 2, filterRect.y, cw, "Error", ref _showError, new Color(0.95f, 0.35f, 0.35f))) changed = true;

            listing.Gap(4f);

            // 数量预设
            var quantRect = listing.GetRect(26f);
            var labelWidth = 55f;
            Widgets.Label(new Rect(quantRect.x, quantRect.y + 3f, labelWidth, 20f), "ModCompatChecker.Quantity".Translate());
            var btnRect = new Rect(quantRect.x + labelWidth, quantRect.y, 70f, 24f);
            string btnLabel = _quantityPreset > 0 ? _quantityPreset.ToString() : (_customQuantity.ToString() + "+");
            if (Widgets.ButtonText(btnRect, btnLabel))
                _showQuantitySelector = !_showQuantitySelector;

            if (_showQuantitySelector)
            {
                listing.Gap(2f);
                var presetsRect = listing.GetRect(22f);
                float px = presetsRect.x;
                foreach (var p in Presets)
                {
                    var pr = new Rect(px, presetsRect.y, 42f, 20f);
                    if (Widgets.ButtonText(pr, p.ToString()))
                    {
                        _quantityPreset = p;
                        _showQuantitySelector = false;
                        changed = true;
                    }
                    px += 46f;
                }
                var cr = new Rect(px, presetsRect.y, 60f, 20f);
                if (Widgets.ButtonText(cr, "ModCompatChecker.Custom".Translate()))
                {
                    _quantityPreset = 0;
                    _showQuantitySelector = false;
                    changed = true;
                }

                if (_quantityPreset == 0)
                {
                    listing.Gap(2f);
                    listing.Label("ModCompatChecker.CustomQuantity".Translate(), -1);
                    var cqStr = listing.TextEntry(_customQuantity.ToString());
                    if (int.TryParse(cqStr, out var cq) && cq >= 1 && cq <= 200)
                    {
                        _customQuantity = cq;
                        changed = true;
                    }
                }
            }

            if (changed) _needsRefresh = true;
            listing.Gap(2f);
        }

        private bool DrawCheckbox(float x, float y, float w, string label, ref bool value, Color color)
        {
            var rect = new Rect(x, y, w, 22f);
            GUI.color = value ? color : new Color(0.3f, 0.3f, 0.3f);
            Widgets.DrawBoxSolid(new Rect(x, y + 4f, 14f, 14f), GUI.color);
            GUI.color = Color.white;
            Widgets.Label(new Rect(x + 18f, y + 2f, w - 20f, 18f), label);
            if (Widgets.ButtonInvisible(rect))
            {
                value = !value;
                return true;
            }
            return false;
        }

        private void DrawQuantitySelector(Listing_Standard listing)
        {
            var ctrlRect = listing.GetRect(24f);
            Widgets.Label(new Rect(ctrlRect.x, ctrlRect.y + 2f, 40f, 20f), "ModCompatChecker.Quantity".Translate());
            if (Widgets.ButtonText(new Rect(ctrlRect.x + 42f, ctrlRect.y, 36f, 22f), "-"))
                _selectedCount = Math.Max(1, _selectedCount - 1);
            Widgets.Label(new Rect(ctrlRect.x + 82f, ctrlRect.y + 2f, 30f, 20f), _selectedCount.ToString());
            if (Widgets.ButtonText(new Rect(ctrlRect.x + 108f, ctrlRect.y, 36f, 22f), "+"))
                _selectedCount = Math.Min(_maxSelectable, _selectedCount + 1);
            if (Widgets.ButtonText(new Rect(ctrlRect.x + 152f, ctrlRect.y, 45f, 22f), "ModCompatChecker.SelectAll".Translate()))
                _selectedCount = _maxSelectable;
            Widgets.Label(new Rect(ctrlRect.x + 205f, ctrlRect.y + 2f, 100f, 20f), "(" + "ModCompatChecker.MaxOf".Translate() + _maxSelectable + ")");
        }

        private void DrawSourceTab(Rect rect, string label, ErrorSource source)
        {
            bool active = _source == source;
            GUI.color = active ? new Color(0.3f, 0.55f, 0.3f) : new Color(0.18f, 0.18f, 0.18f);
            Widgets.DrawBoxSolid(rect, GUI.color);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, label);
            Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonInvisible(rect))
            {
                if (_source != source)
                {
                    _source = source;
                    _needsRefresh = true;
                }
            }
        }

        private void RefreshErrorsNow()
        {
            _errors.Clear();
            _selectedCount = 1;
            DoLoadErrors();
        }

        private void DoLoadErrors()
        {
            if (_source == ErrorSource.Clipboard)
            {
                _errors.Add(new ErrorItem { Level = "INFO", Timestamp = "ModCompatChecker.Clipboard".Translate(),
                    Summary = "ModCompatChecker.ClipboardHint".Translate() });
                _maxSelectable = 1;
            }
            else if (_source == ErrorSource.Console)
            {
                LoadFromConsole();
            }
            else
            {
                LoadFromLogFile();
            }
            _maxSelectable = Math.Max(1, _errors.Count);
            _selectedCount = Math.Min(_selectedCount, _maxSelectable);
        }

        private void LoadFromConsole()
        {
            try
            {
                int totalToFetch = _quantityPreset > 0 ? _quantityPreset : _customQuantity;
                int fetchCount = Math.Min(totalToFetch + 20, 200);
                var entries = LogCapture.GetRecent(fetchCount, _showInfo, _showWarning, _showError);

                foreach (var entry in entries)
                {
                    string level;
                    switch (entry.Level)
                    {
                        case LogCapture.LogLevel.Error: level = "ERR"; break;
                        case LogCapture.LogLevel.Warning: level = "WRN"; break;
                        default: level = "INF"; break;
                    }

                    _errors.Add(new ErrorItem
                    {
                        Level = level,
                        Timestamp = entry.Timestamp,
                        Summary = entry.Message.Length > 110 ? entry.Message.Substring(0, 110) : entry.Message,
                        FullText = entry.Message
                    });
                }

                // 限制显示数量到用户选择的数量
                while (_errors.Count > totalToFetch)
                    _errors.RemoveAt(0);
            }
            catch (Exception ex)
            {
                Log.Warning("[ModCompatChecker] Console log load failed: " + ex.Message);
                LoadFromLogFile();
            }
        }

        private void LoadFromLogFile()
        {
            try
            {
                var logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low",
                    "Ludeon Studios", "RimWorld by Ludeon Studios", "Player.log");
                if (!File.Exists(logPath)) return;

                var fileInfo = new FileInfo(logPath);
                if (fileInfo.Length > 50 * 1024 * 1024)
                {
                    LoadFromLogFileTail(logPath);
                    return;
                }
                var lines = File.ReadAllLines(logPath);
                for (int i = lines.Length - 1; i >= 0; i--)
                {
                    var line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line)) continue;
                    if (!line.Contains("Exception:") && !line.StartsWith("Error in") && !line.Contains("Error"))
                        continue;

                    var stackLines = new List<string>();
                    for (int j = i; j < lines.Length && j < i + 12; j++)
                        stackLines.Add(lines[j]);
                    var tsMatch = TimestampRegex.Match(line);
                    _errors.Add(new ErrorItem
                    {
                        Level = "ERR",
                        Timestamp = tsMatch.Success ? tsMatch.Groups[1].Value : "??:??",
                        Summary = line.Length > 110 ? line.Substring(0, 110) : line,
                        FullText = string.Join("\n", stackLines)
                    });
                    if (_errors.Count >= 50) break;
                }
                _errors.Reverse();
            }
            catch (Exception ex)
            {
                Log.Warning("[ModCompatChecker] LogFile load failed: " + ex.Message);
            }
        }

        private void StartAnalyze()
        {
            var settings = ModCompatChecker.ModCompatMod.Instance?.Settings;
            if (settings == null || !settings.IsAIConfigured()) return;

            var r = Res;
            lock (_lock) { if (r.IsAnalyzing) return; r.IsAnalyzing = true; }
            r.CancelRequested = false;
            r.AiResult = "";
            r.FollowUpAnswer = "";
            r.DetectedDeps.Clear();
            r.ShowFollowUp = false;
            r.ShowSteamSearch = false;
            r.Stopwatch.Restart();

            string errorText;
            if (_source == ErrorSource.Clipboard)
            {
                errorText = string.IsNullOrEmpty(_clipboardText)
                    ? GUIUtility.systemCopyBuffer ?? ""
                    : _clipboardText;
                if (string.IsNullOrEmpty(errorText))
                {
                    r.AiResult = "ModCompatChecker.ClipboardEmptyDetail".Translate();
                    lock (_lock) { r.IsAnalyzing = false; }
                    return;
                }
            }
            else
            {
                var selected = new List<string>();
                int take = Math.Min(_selectedCount, _errors.Count);
                for (int i = Math.Max(0, _errors.Count - take); i < _errors.Count; i++)
                    selected.Add("[" + _errors[i].Level + " " + _errors[i].Timestamp + "] " + _errors[i].FullText);
                errorText = string.Join("\n---\n", selected);
            }

            if (string.IsNullOrWhiteSpace(errorText))
            {
                r.AiResult = "ModCompatChecker.NoErrorContent".Translate();
                lock (_lock) { r.IsAnalyzing = false; }
                return;
            }

            var mi = settings.GetSelectedModelInfo();
            var cost = CostEstimator.Estimate(errorText, mi.Id, 300);
            r.CostInfo = CostEstimator.FormatCost(cost);

            var ep = settings.GetEndpointAndProvider(mi);
            var mid = settings.IsCustomModel() ? settings.CustomModelId : mi.Id;
            TimeoutSeconds = settings.AnalysisTimeoutSeconds;

            var sr = r; // capture for closure
            var thread = new Thread(() =>
            {
                try
                {
                    var prompt = PromptBuilder.BuildErrorAnalysisPrompt(errorText, new ConflictReport(), PromptBuilder.GetPromptLanguage());
                    var result = AIService.CallAPIWithTimeout(
                        ep.endpoint, settings.APIKey, mid, prompt, ep.provider,
                        TimeoutSeconds, ref sr.CancelRequested);
                    sr.Stopwatch.Stop();
                    if (!_disposed) lock (_lock)
                    {
                        sr.AiResult = result;
                        if (sr.CancelRequested) sr.AiResult = "ModCompatChecker.AnalysisCancelledNL".Translate() + sr.AiResult;
                        if (sr.Stopwatch.Elapsed.TotalSeconds > TimeoutSeconds * 0.8)
                            sr.AiResult += "\n\n耗时 " + sr.Stopwatch.Elapsed.TotalSeconds.ToString("F1") + "ModCompatChecker.SecondsLabel".Translate();
                        // 从AI分析结果中提取关键词用于Steam搜索
                        sr.DetectedDeps = ExtractKeywordsFromAI(result);
                    }
                }
                catch (Exception ex)
                {
                    if (!_disposed) lock (_lock) { sr.AiResult = "ModCompatChecker.AIAnalysisFailedPrefix".Translate() + ex.Message; }
                    sr.Stopwatch.Stop();
                }
                if (!_disposed) lock (_lock) { sr.IsAnalyzing = false; }
            }) { IsBackground = true };
            thread.Start();
        }

        // 从AI分析结果中提取关键词用于Steam搜索
        private List<string> ExtractKeywordsFromAI(string aiResult)
        {
            var deps = new List<string>();
            if (string.IsNullOrEmpty(aiResult)) return deps;

            // 1. 正则提取可能的MOD名
            var patterns = new Regex[]
            {
                new Regex(@"(?:缺少|缺失|需要|前置|依赖|require|miss|depend)[^\n]{0,20}?[\uff1a:\s]*['`""]?([\w\s\.\-_\u4e00-\u9fff]{3,40})['`""]?", RegexOptions.IgnoreCase),
                new Regex(@"\b([a-zA-Z0-9_]+(?:\.[a-zA-Z0-9_]+){2,4})\b"),
                new Regex(@"['`""]?([\w\s\.\-_\u4e00-\u9fff]{3,40})['`""]?\s*(?:not\s+(?:loaded|found|installed)|未加载|未找到|未安装|找不到)", RegexOptions.IgnoreCase),
            };

            var seen = new HashSet<string>();
            foreach (var pattern in patterns)
            {
                foreach (Match m in pattern.Matches(aiResult))
                {
                    var name = m.Groups[1].Value.Trim();
                    if (name.Length >= 3 && name.Length <= 80 && !seen.Contains(name.ToLower()))
                    {
                        if (!int.TryParse(name, out _) && !IsCommonFramework(name))
                        {
                            deps.Add(name);
                            seen.Add(name.ToLower());
                        }
                    }
                }
            }

            // 2. 如果正则没匹配到，尝试提取AI建议中的MOD列表
            if (deps.Count == 0)
            {
                var listMatch = Regex.Match(aiResult, @"(?:MOD|mod|模组)[\s\uff1a:]*(.+?)(?:\n\n|\n$|$)", RegexOptions.Singleline);
                if (listMatch.Success)
                {
                    var items = listMatch.Groups[1].Value.Split(new[] { ',', ';', '\uff0c', '\uff1b', '\n' });
                    foreach (var item in items)
                    {
                        var trimmed = item.Trim().TrimStart('-', '*', '\u2022').Trim();
                        if (trimmed.Length >= 2 && trimmed.Length <= 50 && !seen.Contains(trimmed.ToLower()))
                        {
                            if (!int.TryParse(trimmed, out _) && !IsCommonFramework(trimmed))
                            {
                                deps.Add(trimmed);
                                seen.Add(trimmed.ToLower());
                            }
                        }
                    }
                }
            }

            return deps;
        }

        private bool IsCommonFramework(string name)
        {
            var prefixes = new[] { "System.", "UnityEngine.", "Unity.", "Mono.", "Microsoft.", "Verse.", "RimWorld.", "Ludeon." };
            foreach (var p in prefixes)
                if (name.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                    return true;
            var common = new HashSet<string> { "error", "exception", "warning", "null", "true", "false", "错误", "异常", "警告", "空" };
            return common.Contains(name.ToLower());
        }

        private void DoFollowUp()
        {
            if (string.IsNullOrEmpty(_followUpQuestion)) return;
            var settings = ModCompatChecker.ModCompatMod.Instance?.Settings;
            if (settings == null || !settings.IsAIConfigured()) return;

            var r = Res;
            lock (_lock) { if (r.IsFollowUpAnalyzing) return; r.IsFollowUpAnalyzing = true; }
            r.FollowUpAnswer = "";

            var mi = settings.GetSelectedModelInfo();
            var ep = settings.GetEndpointAndProvider(mi);
            var mid = settings.IsCustomModel() ? settings.CustomModelId : mi.Id;
            var prompt = "ModCompatChecker.PreviousAnalysisResult".Translate() + r.AiResult + "\n\n" + "ModCompatChecker.UserFollowUp".Translate() + _followUpQuestion + "\n\n" + "ModCompatChecker.FollowUpPrompt".Translate();

            var sr = r;
            var cancel = false;
            var thread = new Thread(() =>
            {
                try
                {
                    var result = AIService.CallAPIWithTimeout(
                        ep.endpoint, settings.APIKey, mid, prompt, ep.provider, 15, ref cancel);
                    if (!_disposed) lock (_lock) { sr.FollowUpAnswer = result; }
                }
                catch (Exception ex)
                {
                    if (!_disposed) lock (_lock) { sr.FollowUpAnswer = "ModCompatChecker.FollowUpFailed".Translate() + ex.Message; }
                }
                if (!_disposed) lock (_lock) { sr.IsFollowUpAnalyzing = false; }
            }) { IsBackground = true };
            thread.Start();
        }

        private Color GetLevelColor(string level)
        {
            switch (level)
            {
                case "ERR": return new Color(0.9f, 0.35f, 0.35f);
                case "WRN": return new Color(0.9f, 0.75f, 0.3f);
                default: return new Color(0.6f, 0.6f, 0.6f);
            }
        }

        private void LoadFromLogFileTail(string logPath)
        {
            try
            {
                var lines = File.ReadAllLines(logPath);
                int start = Math.Max(0, lines.Length - 1000);
                int count = 0;
                for (int i = lines.Length - 1; i >= start; i--)
                {
                    var line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line)) continue;
                    if (!line.Contains("Exception:") && !line.StartsWith("Error in") && !line.Contains("Error"))
                        continue;
                    var stackLines = new List<string>();
                    for (int j = i; j < lines.Length && j < i + 12; j++)
                        stackLines.Add(lines[j]);
                    var tsMatch = TimestampRegex.Match(line);
                    _errors.Add(new ErrorItem
                    {
                        Level = "ERR",
                        Timestamp = tsMatch.Success ? tsMatch.Groups[1].Value : "??:??",
                        Summary = line.Length > 110 ? line.Substring(0, 110) : line,
                        FullText = string.Join("\n", stackLines)
                    });
                    count++;
                    if (count >= 50) break;
                }
                _errors.Reverse();
            }
            catch { }
        }

        private string Truncate(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLen) return text;
            return text.Substring(0, maxLen) + "...";
        }

        private class ErrorItem
        {
            public string Level;
            public string Timestamp;
            public string Summary;
            public string FullText;
            public bool Selected;
        }
    }
}




