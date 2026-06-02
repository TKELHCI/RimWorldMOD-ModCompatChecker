using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading;
using System.Xml;
using ModCompatChecker.AI;
using ModCompatChecker.Core;
using ModCompatChecker.Patches;
using UnityEngine;
using Verse;

namespace ModCompatChecker.UI
{
    public class UnifiedWindow : Window
    {
        private Vector2 _scroll = Vector2.zero;
        private readonly object _lock = new object();
        private bool _disposed; private static int _cachedWorldId = -1; private static int _worldCheckFrame;
        private bool _showSettings = true, _showCompat = true, _showErrorSection, _showTools, _spamChecking, _showAdvanced;
        private readonly SharedSettingsUI.UIState _uiState = new SharedSettingsUI.UIState();

        public override Vector2 InitialSize
{
    get
    {
        float h = 780f;
        ConflictReport r; lock (_lock) { r = _report; }
        if (r != null && _hasScanned)
        {
            int total = r.TotalConflictCount;
            if (total > 10) h = Mathf.Min(1100f, 780f + (total - 10) * 18f);
        }
        return new Vector2(1000f, h);
    }
}

        public UnifiedWindow()
        {
            doCloseX = true;
            doCloseButton = true;
            closeOnAccept = false;
            absorbInputAroundWindow = true;
            draggable = true;
            resizeable = true;
            layer = WindowLayer.Dialog;
        }

        public override void PreClose()
        {
            _disposed = true;
            _uiState.Disposed = true;
            base.PreClose();
        }

        public override void DoWindowContents(Rect inRect)
        {
            var settings = ModCompatChecker.ModCompatMod.Instance?.Settings;
            if (settings == null) return;
            if (_worldCheckFrame != Time.frameCount) CheckCache();

            // Wider scrollbars for better usability
            GUI.skin.verticalScrollbar.fixedWidth = 24f;
            GUI.skin.verticalScrollbarThumb.fixedWidth = 20f;

            var listing = new Listing_Standard();
            listing.Begin(inRect);

            Text.Font = GameFont.Medium;
            listing.Label("ModCompatChecker.MainTitle".Translate(), -1);
            Text.Font = GameFont.Small;
            listing.Gap(6f);

            var scrollRect = listing.GetRect(inRect.height - 60f);
                        float innerContentH = 3200f;
            ConflictReport repH; lock (_lock) { repH = _report; }
            if (repH != null && _hasScanned)
                innerContentH = Mathf.Max(3200f, 800f + repH.TotalConflictCount * 130f);
            Widgets.BeginScrollView(scrollRect, ref _scroll, new Rect(0f, 0f, scrollRect.width - 28f, innerContentH));

            var inner = new Listing_Standard();
            inner.Begin(new Rect(0f, 0f, scrollRect.width - 28f, innerContentH));

            DrawSectionHeader(inner, ref _showSettings, "ModCompatChecker.UI179".Translate(), new Color(0.18f, 0.38f, 0.55f));
            if (_showSettings)
            {
                SharedSettingsUI.DrawModelSelector(inner, settings, _uiState);
                inner.Gap(8f);
                SharedSettingsUI.DrawAPISettings(inner, settings, _uiState);
                inner.Gap(6f);
                SharedSettingsUI.DrawTestConnection(inner, settings, _uiState);
                inner.Gap(14f);
            }

            DrawSectionHeader(inner, ref _showCompat, "ModCompatChecker.UI180".Translate(), new Color(0.28f, 0.38f, 0.18f));
            if (_showCompat)
            {
                DrawCompatibilitySection(inner, settings);
                inner.Gap(14f);
            }

            DrawSectionHeader(inner, ref _showErrorSection, "ModCompatChecker.UI181".Translate(), new Color(0.50f, 0.28f, 0.18f));
            if (_showErrorSection)
                DrawErrorSection(inner, settings);

            DrawSectionHeader(inner, ref _showTools, "ModCompatChecker.Tools".Translate(), new Color(0.35f, 0.25f, 0.50f));
            if (_showTools) {
                listing.Label("ModCompatChecker.SpamDetect".Translate(), -1);
                if (Widgets.ButtonText(listing.GetRect(28f), "ModCompatChecker.CheckSpam".Translate())) { Core.SpamDetector.CheckForSpam(); _spamChecking = true; }
                if (_spamChecking && Core.SpamDetector.ActiveAlerts.Count > 0) { foreach (var a in Core.SpamDetector.ActiveAlerts) { GUI.color = Color.red; Widgets.Label(listing.GetRect(20f), "[" + a.Count + "x] " + a.NormalizedMessage); GUI.color = Color.white; } }
                listing.Gap(4f);
                if (Widgets.ButtonText(listing.GetRect(24f), "ModCompatChecker.OpenLogFolder".Translate())) { var p = Core.SpamDetector.GetLogFolderPath(); if (p.Length > 0) System.Diagnostics.Process.Start("explorer.exe", p); }
                listing.Gap(8f);
            }
            inner.End();
            Widgets.EndScrollView();

            // ── Quick-ask bar (fixed at bottom) ──
            listing.Gap(4f);
            var quickRect = listing.GetRect(28f);
            Widgets.DrawBoxSolid(new Rect(quickRect.x - 2f, quickRect.y - 2f, quickRect.width + 4f, quickRect.height + 4f),
                new Color(0.12f, 0.12f, 0.18f));
            GUI.color = new Color(0.7f, 0.7f, 0.9f);
            Widgets.Label(new Rect(quickRect.x + 4f, quickRect.y + 4f, 70f, 20f), "ModCompatChecker.UI182".Translate());
            GUI.color = Color.white;
            _quickAsk = GUI.TextField(new Rect(quickRect.x + 78f, quickRect.y + 2f, quickRect.width - 158f, 24f), _quickAsk);
            bool qRunning; lock (_lock) { qRunning = _quickRunning; }
            bool hasAPI = settings.IsAIConfigured();
            if (qRunning)
                Widgets.Label(new Rect(quickRect.x + quickRect.width - 76f, quickRect.y + 4f, 72f, 20f), "ModCompatChecker.Analyzing".Translate());
            else if (!hasAPI)
            {
                GUI.color = new Color(0.4f, 0.4f, 0.4f);
                Widgets.Label(new Rect(quickRect.x + quickRect.width - 76f, quickRect.y + 4f, 72f, 20f), "ModCompatChecker.NeedAPI".Translate());
                GUI.color = Color.white;
            }
            else if (Widgets.ButtonText(new Rect(quickRect.x + quickRect.width - 76f, quickRect.y + 2f, 72f, 24f), "ModCompatChecker.Send".Translate()))
                StartQuickAsk(settings);
            if (!string.IsNullOrEmpty(_quickAnswer))
            {
                listing.Gap(2f);
                var qaHeight = Mathf.Min(Text.CalcHeight(_quickAnswer, quickRect.width - 16f) + 12f, 200f);
                var qaRect = listing.GetRect(qaHeight);
                Widgets.DrawBoxSolid(qaRect, new Color(0.08f, 0.08f, 0.16f));
                var contentH = Text.CalcHeight(_quickAnswer, qaRect.width - 16f) + 8f;
                Widgets.BeginScrollView(qaRect, ref _quickScroll, new Rect(0f, 0f, qaRect.width - 16f, contentH));
                Widgets.Label(new Rect(0f, 0f, qaRect.width - 16f, contentH), _quickAnswer);
                Widgets.EndScrollView();
            }

            listing.End();
        }

        private void DrawSectionHeader(Listing_Standard lst, ref bool expanded, string title, Color? activeColor = null)
        {
            var rect = lst.GetRect(30f);
            var ac = activeColor ?? new Color(0.22f, 0.45f, 0.22f); GUI.color = expanded ? ac : new Color(0.14f, 0.14f, 0.14f);
            Widgets.DrawBoxSolid(rect, GUI.color);
            GUI.color = Color.white;
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 5f, rect.width - 20f, 20f),
                (expanded ? "▼ " : "▶ ") + title);
            if (Widgets.ButtonInvisible(rect))
                expanded = !expanded;
            if (expanded)
                lst.Gap(6f);
        }

        private void CheckCache()
        {
            int worldId = -1;
            try { if (Current.Game != null) worldId = Current.Game.GetHashCode(); } catch { }
            string modFingerprint = GetModFingerprint();
            if (worldId != _cachedWorldId || _lastFingerprint != modFingerprint)
            {
                _cachedReport = null;
                _cachedHasScanned = false;
                _cachedAI.Clear();
                _cachedErrors.Clear();
                _cachedErrorResult.Clear();
                _cachedDeps.Clear(); _metaCache.Clear();
                _aiResults.Clear(); _aiExpanded.Clear();
                _report = null; _hasScanned = false;
                _cachedWorldId = worldId;
                _lastFingerprint = modFingerprint;
                // Try load from disk cache
                TryLoadScanCache();
            }
            if (_cachedHasScanned)
            {
                _report = _cachedReport;
                _hasScanned = true;
                foreach (var kv in _cachedAI) _aiResults[kv.Key] = kv.Value;
            }
            if (_cachedErrors.Count > 0)
            {
                _errorEntries.Clear();
                _errorEntries.AddRange(_cachedErrors);
                foreach (var kv in _cachedErrorResult)
                    _errRes[kv.Key] = kv.Value;
                foreach (var kv in _cachedDeps)
                    _errDep[kv.Key] = kv.Value;
                _needsRefresh = false;
            }
            _worldCheckFrame = Time.frameCount;
        }

        private static string _lastFingerprint = "";
        private static ConflictReport _diskCachedReport;

        private static string GetModFingerprint()
        {
            try
            {
                var ids = LoadedModManager.RunningModsListForReading
                    .Select(m => m.PackageId ?? m.Name)
                    .OrderBy(s => s);
                return string.Join(",", ids);
            }
            catch { return ""; }
        }

        private static void SaveScanCache(ConflictReport report)
        {
            try
            {
                var dir = ModCompatChecker.ModCompatMod.Instance?.Content?.RootDir;
                if (dir == null) return;
                var path = Path.Combine(dir, "Assemblies", "ScanCache.json");
                var sb = new System.Text.StringBuilder();
                sb.Append("{\"fp\":\"").Append(GetModFingerprint()).Append("\",");
                sb.Append("\"h\":" ).Append(report.HarmonyConflicts.Count).Append(",");
                sb.Append("\"d\":").Append(report.DefConflicts.Count).Append(",");
                sb.Append("\"i\":").Append(report.DependencyIssues.Count).Append(",");
                sb.Append("\"mods\":").Append(report.TotalLoadedMods).Append("}");
                File.WriteAllText(path, sb.ToString());
            }
            catch { /* optional, not critical */ }
        }

        private void TryLoadScanCache()
        {
            try
            {
                var dir = ModCompatChecker.ModCompatMod.Instance?.Content?.RootDir;
                if (dir == null) return;
                var path = Path.Combine(dir, "Assemblies", "ScanCache.json");
                if (!File.Exists(path)) return;
                var json = File.ReadAllText(path);
                // Simple parse: check fingerprint matches
                var fpMatch = System.Text.RegularExpressions.Regex.Match(json, "\"fp\":\"([^\"]+)\"");
                if (!fpMatch.Success || fpMatch.Groups[1].Value != GetModFingerprint()) return;
                // Fingerprint matches - restore counts (full report would need re-scan for details)
                // Just mark that we had a valid scan so the UI shows "Rescan" instead of "Start Scan"
                _diskCachedReport = new ConflictReport();
                var hMatch = System.Text.RegularExpressions.Regex.Match(json, "\"h\":(\\d+)");
                var dMatch = System.Text.RegularExpressions.Regex.Match(json, "\"d\":(\\d+)");
                var iMatch = System.Text.RegularExpressions.Regex.Match(json, "\"i\":(\\d+)");
                var mMods = System.Text.RegularExpressions.Regex.Match(json, "\"mods\":(\\d+)");
                if (hMatch.Success) _diskCachedReport.HarmonyConflicts = new List<Core.HarmonyConflict>(new Core.HarmonyConflict[int.Parse(hMatch.Groups[1].Value)]);
                if (dMatch.Success) _diskCachedReport.DefConflicts = new List<Core.DefConflict>(new Core.DefConflict[int.Parse(dMatch.Groups[1].Value)]);
                if (iMatch.Success) _diskCachedReport.DependencyIssues = new List<Core.DependencyIssue>(new Core.DependencyIssue[int.Parse(iMatch.Groups[1].Value)]);
                if (mMods.Success) _diskCachedReport.TotalLoadedMods = int.Parse(mMods.Groups[1].Value);
                _cachedReport = _diskCachedReport;
                _cachedHasScanned = true;
            }
            catch { /* fallback: user rescans */ }
        }

        // ── Compatibility Section ──
        private static ConflictReport _cachedReport; private static bool _cachedHasScanned; private static readonly Dictionary<int, string> _cachedAI = new Dictionary<int, string>(); private static readonly List<ErrorEntry> _cachedErrors = new List<ErrorEntry>(); private static readonly Dictionary<ErrorSource, string> _cachedErrorResult = new Dictionary<ErrorSource, string>(); private static readonly Dictionary<ErrorSource, List<string>> _cachedDeps = new Dictionary<ErrorSource, List<string>>(); private static readonly Dictionary<string, (string url, string time, string ver)> _metaCache = new Dictionary<string, (string, string, string)>(); private enum CompatTab { Harmony, Def, Dependency }
        private CompatTab _compatTab = CompatTab.Harmony;
        private ConflictReport _report;
        private bool _hasScanned, _isScanning;
        private readonly Dictionary<int, string> _aiResults = new Dictionary<int, string>(); private readonly Dictionary<int, bool> _aiExpanded = new Dictionary<int, bool>();
        private readonly HashSet<int> _pendingAI = new HashSet<int>();
        private bool _isBatchAnalyzing;
        private string _batchStatus = "";

        private void DrawCompatibilitySection(Listing_Standard listing, ModCompatSettings settings)
        {
            bool scanning, scanned;
            lock (_lock) { scanning = _isScanning; scanned = _hasScanned; }
            bool hasAPI = settings.IsAIConfigured();

            var btnRect = listing.GetRect(28f);
            if (!scanning)
            {
                if (Widgets.ButtonText(new Rect(btnRect.x, btnRect.y, 110f, 26f),
                    scanned ? "ModCompatChecker.Rescan".Translate() : "ModCompatChecker.StartScan".Translate()))
                    StartScan();
            }
            else
            {
                Widgets.Label(new Rect(btnRect.x, btnRect.y + 4f, 110f, 20f), "ModCompatChecker.Scanning".Translate());
            }

            if (scanned)
            {
                ConflictReport report;
                lock (_lock) { report = _report; }
                if (report != null && report.HasConflicts && settings.IsAIConfigured())
                {
                    bool busy; string status;
                    lock (_lock) { busy = _isBatchAnalyzing; status = _batchStatus; }
                    if (!hasAPI)
                    {
                        GUI.color = new Color(0.4f, 0.4f, 0.4f);
                        Widgets.Label(new Rect(btnRect.x + 120f, btnRect.y + 4f, 200f, 20f), "AI功能需配置API Key");
                        GUI.color = Color.white;
                    }
                    else if (busy)
                        Widgets.Label(new Rect(btnRect.x + 120f, btnRect.y + 4f, 200f, 20f), status);
                    else if (Widgets.ButtonText(new Rect(btnRect.x + 120f, btnRect.y, 130f, 26f), "AI 批量分析"))
            listing.Gap(4f);
            if (listing.ButtonText("ModCompatChecker.CheckUpdates".Translate()))
            {
                _updateVersions = Core.UpdateChecker.GetLocalModVersions();
                Core.UpdateChecker.CheckWorkshopUpdates(_updateVersions, (result) => { if (!_disposed) lock (_lock) { _updateVersions = result; _showUpdates = true; } });
                _showUpdates = true;
            }

                        StartBatchAnalysis(settings);
                }
            }



            // === Relationship Chain ===
            listing.Gap(4f);
            GUI.color = new Color(0.2f, 0.4f, 0.6f);
            if (Widgets.ButtonText(listing.GetRect(28f), "ModCompatChecker.RelationshipChain".Translate()))
            {
                ConflictReport repR; lock (_lock) { repR = _report; }
                if (repR != null && repR.HasConflicts)
                {
                    var conflictMods = Core.RelationshipAnalyzer.GetAllConflictingMods(repR);
                    var modList = new List<string>(conflictMods);
                    modList.Sort();
                    var floatOpts = new List<FloatMenuOption>();
                    foreach (var m in modList)
                    {
                        string mn = m;
                        floatOpts.Add(new FloatMenuOption(mn, () => {
                            ConflictReport rep; lock (_lock) { rep = _report; }
                            if (rep != null)
                            {
                                _relationship = Core.RelationshipAnalyzer.Analyze(mn, rep);
                                _relSelectedMod = mn;
                                _relOpen = true;
                                _relDepends = true; _relDepended = true; _relHarmony = true; _relDef = true; _relOrder = true;
                            }
                        }));
                    }
                    if (floatOpts.Count > 0) Find.WindowStack.Add(new FloatMenu(floatOpts));
                }
            }
            GUI.color = Color.white;

            // === Update Check Result ===
            if (_showUpdates && _updateVersions != null)
            {
                listing.Gap(4f);
                GUI.color = new Color(0.25f, 0.4f, 0.6f);
                Widgets.DrawBoxSolid(listing.GetRect(2f), GUI.color);
                GUI.color = Color.white;
                listing.Gap(4f);
                listing.Label("ModCompatChecker.UpdateCheckResult".Translate() + " (" + _updateVersions.Count + " MOD)", -1);
                var updatesFound = _updateVersions.FindAll(v => v.HasUpdate);
                if (updatesFound.Count > 0)
                {
                    GUI.color = new Color(0.9f, 0.6f, 0.1f);
                    listing.Label("ModCompatChecker.UpdatesAvailable".Translate() + " " + updatesFound.Count, -1);
                    GUI.color = Color.white;
                }
                var ulRect = listing.GetRect(Mathf.Min(_updateVersions.Count * 24f, 200f));
                Widgets.BeginScrollView(ulRect, ref _updateScroll, new Rect(0f, 0f, ulRect.width - 20f, _updateVersions.Count * 24f));
                float uy = 0f;
                foreach (var v in _updateVersions)
                {
                    string status = v.HasUpdate ? "[!]" : "[OK]";
                    GUI.color = v.HasUpdate ? new Color(1f, 0.6f, 0.2f) : new Color(0.4f, 0.7f, 0.4f);
                    Widgets.Label(new Rect(4f, uy, ulRect.width - 30f, 22f), status + " " + v.Name + "   v" + v.LocalVersion + (v.HasUpdate ? " -> " + v.WorkshopVersion : ""));
                    GUI.color = Color.white;

            // === Relationship Display ===
            if (_relOpen && _relationship != null)
            {
                listing.Gap(4f);
                GUI.color = new Color(0.25f, 0.45f, 0.65f);
                Widgets.DrawBoxSolid(listing.GetRect(2f), GUI.color);
                GUI.color = Color.white;
                listing.Gap(4f);
                Widgets.Label(listing.GetRect(24f), "ModCompatChecker.ModRelationTitle".Translate() + "  " + Truncate(_relationship.ModName, 50));

                var relContentHeight = 0f;
                if (_relDepends && _relationship.DependsOn.Count > 0) relContentHeight += 22f + _relationship.DependsOn.Count * 20f;
                if (_relDepended && _relationship.DependedBy.Count > 0) relContentHeight += 22f + _relationship.DependedBy.Count * 20f;
                if (_relHarmony && _relationship.HarmonyConflictsWith.Count > 0) relContentHeight += 22f + _relationship.HarmonyConflictsWith.Count * 20f;
                if (_relDef && _relationship.DefConflictsWith.Count > 0) relContentHeight += 22f + _relationship.DefConflictsWith.Count * 20f;
                if (_relOrder && (_relationship.LoadBefore.Count > 0 || _relationship.LoadAfter.Count > 0)) relContentHeight += 22f + (_relationship.LoadBefore.Count + _relationship.LoadAfter.Count) * 20f;
                if (_relationship.MissingDeps.Count > 0) relContentHeight += 22f + _relationship.MissingDeps.Count * 20f;
                if (_relationship.VersionIssues.Count > 0) relContentHeight += 22f + _relationship.VersionIssues.Count * 20f;

                if (relContentHeight > 0)
                {
                    var rlHeight = Mathf.Min(relContentHeight + 10f, 300f);
                    var rlRect = listing.GetRect(rlHeight);
                    Widgets.BeginScrollView(rlRect, ref _relScroll, new Rect(0f, 0f, rlRect.width - 24f, relContentHeight + 10f));
                    float ry = 0f;

                    DrawRelSection("ModCompatChecker.RelDependsOn".Translate(), ref _relDepends, _relationship.DependsOn, Color.green, ref ry, rlRect.width - 30f);
                    DrawRelSection("ModCompatChecker.RelDependedBy".Translate(), ref _relDepended, _relationship.DependedBy, new Color(0.3f, 0.7f, 1f), ref ry, rlRect.width - 30f);
                    DrawRelSection("ModCompatChecker.RelHarmonyConflict".Translate(), ref _relHarmony, _relationship.HarmonyConflictsWith, new Color(1f, 0.5f, 0.2f), ref ry, rlRect.width - 30f);
                    DrawRelSection("ModCompatChecker.RelDefConflict".Translate(), ref _relDef, _relationship.DefConflictsWith, new Color(0.9f, 0.3f, 0.3f), ref ry, rlRect.width - 30f);
                    DrawRelSection("ModCompatChecker.RelLoadOrder".Translate(), ref _relOrder, _relationship.LoadBefore.Select(l => "-> before " + l).Concat(_relationship.LoadAfter.Select(l => "after -> " + l)).ToList(), new Color(0.8f, 0.8f, 0.3f), ref ry, rlRect.width - 30f);
                    if (_relationship.MissingDeps.Count > 0) DrawRelSection("ModCompatChecker.RelMissing".Translate(), ref _relDepends, _relationship.MissingDeps, Color.red, ref ry, rlRect.width - 30f);

                    Widgets.EndScrollView();
                }
            }

                    uy += 24f;
                }
                Widgets.EndScrollView();
            }

            listing.Gap(6f);

            if (!scanned)
            {
                listing.Label("ModCompatChecker.ScanHint".Translate(), -1);
                return;
            }

            ConflictReport rep;
            lock (_lock) { rep = _report; }
            if (rep == null) return;

            GUI.color = new Color(0.6f, 0.85f, 0.6f);
            listing.Label("ModCompatChecker.ScanCompleteDetail".Translate() + rep.TotalLoadedMods + "ModCompatChecker.ModCountSep".Translate() + rep.TotalConflictCount + "ModCompatChecker.ProblemCount".Translate(), -1);
            GUI.color = Color.white;
            listing.Gap(4f);

            var tabRect = listing.GetRect(28f);
            float tabW = (tabRect.width - 8f) / 3f;
            DrawTabBtn(new Rect(tabRect.x, tabRect.y, tabW, 26f), "Harmony (" + rep.HarmonyConflicts.Count + ")", CompatTab.Harmony);
            DrawTabBtn(new Rect(tabRect.x + tabW + 4f, tabRect.y, tabW, 26f), "Def (" + rep.DefConflicts.Count + ")", CompatTab.Def);
            DrawTabBtn(new Rect(tabRect.x + (tabW + 4f) * 2, tabRect.y, tabW, 26f), "Dependency (" + rep.DependencyIssues.Count + ")", CompatTab.Dependency);
            listing.Gap(6f);

            switch (_compatTab)
            {
                case CompatTab.Harmony:
                    if (rep.HarmonyConflicts.Count == 0) { listing.Label("ModCompatChecker.NoHarmonyConflict".Translate(), -1); break; }
                    for (int i = 0; i < rep.HarmonyConflicts.Count; i++)
                    {
                        if (i > 0)
                        {
                            var sepR = listing.GetRect(16f);
                            GUI.color = new Color(0.3f, 0.3f, 0.3f);
                            Widgets.DrawLineHorizontal(sepR.x, sepR.y + 7f, sepR.width);
                            GUI.color = Color.white;
                        }
                        DrawHarmonyCard(listing, rep.HarmonyConflicts[i], i);
                    }
                    break;
                case CompatTab.Def:
                    if (rep.DefConflicts.Count == 0) { listing.Label("ModCompatChecker.NoDefConflict".Translate(), -1); break; }
                    for (int i = 0; i < rep.DefConflicts.Count; i++)
                    {
                        if (i > 0)
                        {
                            var sepR = listing.GetRect(16f);
                            GUI.color = new Color(0.3f, 0.3f, 0.3f);
                            Widgets.DrawLineHorizontal(sepR.x, sepR.y + 7f, sepR.width);
                            GUI.color = Color.white;
                        }
                        DrawDefCard(listing, rep.DefConflicts[i], i + 1000);
                    }
                    break;
                case CompatTab.Dependency:
                    if (rep.DependencyIssues.Count == 0) { listing.Label("ModCompatChecker.NoDepIssue".Translate(), -1); break; }
                    for (int i = 0; i < rep.DependencyIssues.Count; i++)
                    {
                        if (i > 0)
                        {
                            var sepR = listing.GetRect(16f);
                            GUI.color = new Color(0.3f, 0.3f, 0.3f);
                            Widgets.DrawLineHorizontal(sepR.x, sepR.y + 7f, sepR.width);
                            GUI.color = Color.white;
                        }
                        DrawDependencyCard(listing, rep.DependencyIssues[i], i + 2000);
                    }
                    break;
            }
        }

        private void DrawTabBtn(Rect rect, string label, CompatTab tab)
        {
            GUI.color = _compatTab == tab ? new Color(0.3f, 0.6f, 0.3f) : new Color(0.2f, 0.2f, 0.2f);
            Widgets.DrawBoxSolid(rect, GUI.color);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, label);
            Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonInvisible(rect))
                _compatTab = tab;
        }

        private void DrawCardBase(Listing_Standard listing, Color riskColor, string riskLabel,
            string title, string detail1, string detail2, int key)
        {
            GUI.color = riskColor;
            listing.Label("▌ " + riskLabel + "  " + title, -1);
            GUI.color = Color.white;
            if (!string.IsNullOrEmpty(detail1))
                listing.Label("     " + detail1, -1);
            if (!string.IsNullOrEmpty(detail2))
                listing.Label("     " + detail2, -1);

            var settings = ModCompatChecker.ModCompatMod.Instance?.Settings;
            bool hasAI = settings != null && settings.IsAIConfigured();
            var btnRow = listing.GetRect(26f);
            if (hasAI)
            {
                bool pending;
                lock (_lock) { pending = _pendingAI.Contains(key); }
                if (pending)
                    Widgets.Label(new Rect(btnRow.x + 20f, btnRow.y + 2f, 120f, 20f), "ModCompatChecker.Analyzing".Translate());
                else if (Widgets.ButtonText(new Rect(btnRow.x + 20f, btnRow.y, 120f, 24f), "ModCompatChecker.AIAnalyze".Translate()))
                    StartSingleAnalysis(key, settings);
            }
            else
            {
                GUI.color = new Color(0.4f, 0.4f, 0.4f);
                Widgets.Label(new Rect(btnRow.x + 20f, btnRow.y + 2f, 200f, 20f), "ModCompatChecker.AINeedsAPIKeyDetail".Translate());
                GUI.color = Color.white;
            }

            string result;
            lock (_lock) { _aiResults.TryGetValue(key, out result); }
            if (!string.IsNullOrEmpty(result))
            {
                bool expanded;
                lock (_lock) { _aiExpanded.TryGetValue(key, out expanded); }
                var hdr = listing.GetRect(24f);
                bool hover = Mouse.IsOver(hdr);
                GUI.color = hover ? new Color(0.25f, 0.5f, 0.25f) : new Color(0.15f, 0.35f, 0.15f);
                Widgets.DrawBoxSolid(hdr, GUI.color);
                GUI.color = Color.white;
                string arrow = expanded ? "▼" : "▶";
                string preview = GetFirstLine(result);
                Widgets.Label(new Rect(hdr.x + 8f, hdr.y + 2f, hdr.width - 16f, 20f),
                    arrow + " [AI] " + Truncate(preview, 70));
                if (Widgets.ButtonInvisible(hdr))
                {
                    lock (_lock) { _aiExpanded[key] = !expanded; }
                }
                if (expanded)
                {
                    listing.Gap(2f);
                    float txtH = Text.CalcHeight(result, hdr.width - 16f) + 16f;
                    var fullRect = listing.GetRect(Mathf.Min(250f, txtH));
                    Widgets.DrawBoxSolid(fullRect, new Color(0.05f, 0.08f, 0.05f, 0.9f));
                    GUI.color = new Color(0.85f, 0.85f, 0.85f);
                    Widgets.Label(new Rect(fullRect.x + 6f, fullRect.y + 4f, fullRect.width - 12f, fullRect.height - 8f), result);
                    GUI.color = Color.white;
                    listing.Gap(2f);
                }
            }
            listing.Gap(10f);
        }

        
        private static (string steamUrl, string updateTime, string version) GetModMeta(string packageId, string modName)
        {
            string url = null, time = "", ver = "";
            var cacheKey = packageId ?? modName ?? "";
            if (!string.IsNullOrEmpty(cacheKey) && _metaCache.TryGetValue(cacheKey, out var cached))
                return cached;
            try
            {
                var mod = LoadedModManager.RunningMods.FirstOrDefault(m =>
                    (!string.IsNullOrEmpty(m.PackageId) && m.PackageId.Equals(packageId, StringComparison.OrdinalIgnoreCase)));
                if (mod == null)
                    mod = LoadedModManager.RunningMods.FirstOrDefault(m =>
                        (!string.IsNullOrEmpty(m.Name) && m.Name.Equals(modName, StringComparison.OrdinalIgnoreCase)));
                if (mod != null)
                {
                    // Steam Workshop ID
                    var wsPath = Path.Combine(mod.RootDir, "About", "PublishedFileId.txt");
                    if (File.Exists(wsPath))
                    {
                        var wsId = File.ReadAllText(wsPath).Trim();
                        if (!string.IsNullOrEmpty(wsId) && long.TryParse(wsId, out _))
                            url = "https://steamcommunity.com/sharedfiles/filedetails/?id=" + wsId;
                        else
                            url = "https://steamcommunity.com/workshop/browse/?appid=294100&searchtext="
                                + Uri.EscapeDataString(modName);
                    }
                    else
                    {
                        url = "https://steamcommunity.com/workshop/browse/?appid=294100&searchtext="
                            + Uri.EscapeDataString(modName);
                    }
                    // Update time from folder
                    try { time = new DirectoryInfo(mod.RootDir).LastWriteTime.ToString("yyyy-MM-dd HH:mm"); } catch { }
                    // Version from About.xml
                    try
                    {
                        var aboutPath = Path.Combine(mod.RootDir, "About", "About.xml");
                        if (File.Exists(aboutPath))
                        {
                            var doc = new System.Xml.XmlDocument();
                            doc.Load(aboutPath);
                            var verNode = doc.SelectSingleNode("//ModMetaData/modVersion");
                            if (verNode != null) ver = verNode.InnerText.Trim();
                        }
                    }
                    catch { }
                }
            }
            catch { }
            var result = (url, time, ver);
            if (!string.IsNullOrEmpty(cacheKey)) _metaCache[cacheKey] = result;
            return result;
        }
        private void DrawHarmonyCard(Listing_Standard listing, HarmonyConflict c, int i)
        {
            DrawCardBase(listing, RiskColor(c.Risk), RiskLabel(c.Risk),
                c.Summary ?? (c.ModNameA + " vs " + c.ModNameB),
                "ModCompatChecker.Target".Translate() + c.TargetType + "." + c.TargetMethod,
                "ModCompatChecker.Patch".Translate() + c.ModNameA + "[" + c.PatchTypeA + "] · " + c.ModNameB + "[" + c.PatchTypeB + "]", i);
            DrawModMetaRow(listing, c.ModPackageIdA, c.ModNameA);
            DrawModMetaRow(listing, c.ModPackageIdB, c.ModNameB);
        }

        private void DrawDefCard(Listing_Standard listing, DefConflict c, int i)
        {
            DrawCardBase(listing, RiskColor(c.Risk), RiskLabel(c.Risk),
                c.Summary ?? (c.ModNameA + " vs " + c.ModNameB),
                "ModCompatChecker.DefLabel".Translate() + c.DefType + "/" + c.DefName,
                "ModCompatChecker.XPath".Translate() + Truncate(c.XPathA, 60), i);
            DrawModMetaRow(listing, c.ModPackageIdA, c.ModNameA);
            DrawModMetaRow(listing, c.ModPackageIdB, c.ModNameB);
        }

        
        private void DrawModMetaRow(Listing_Standard listing, string packageId, string modName)
        {
            var (steamUrl, updateTime, version) = GetModMeta(packageId, modName);
            bool hasInfo = !string.IsNullOrEmpty(steamUrl) || !string.IsNullOrEmpty(version) || !string.IsNullOrEmpty(updateTime);
            if (!hasInfo) return;
            var row = listing.GetRect(22f);
            Widgets.Label(new Rect(row.x + 16f, row.y + 1f, row.width * 0.35f, 20f),
                "[MOD] " + Truncate(modName, 30));
            if (!string.IsNullOrEmpty(version))
                Widgets.Label(new Rect(row.x + row.width * 0.38f, row.y + 1f, row.width * 0.18f, 20f),
                    "v" + version);
            if (!string.IsNullOrEmpty(updateTime))
                Widgets.Label(new Rect(row.x + row.width * 0.55f, row.y + 1f, row.width * 0.25f, 20f),
                    updateTime);
            if (!string.IsNullOrEmpty(steamUrl))
            {
                if (Widgets.ButtonText(new Rect(row.x + row.width * 0.82f, row.y, 80f, 20f), "Steam"))
                {
                    try { Process.Start(steamUrl); }
                    catch { try { Application.OpenURL(steamUrl); } catch { } }
                }
            }
        }
        private void DrawDependencyCard(Listing_Standard listing, DependencyIssue iss, int i)
        {
            DrawCardBase(listing, RiskColor(iss.Risk), RiskLabel(iss.Risk),
                iss.Summary ?? iss.ExtraInfo ?? "Unknown",
                iss.RelatedPackageId ?? "",
                "ModCompatChecker.Type".Translate() + iss.Type + " · " + iss.ModName, i);

            var settings = ModCompatChecker.ModCompatMod.Instance?.Settings;
            if (!string.IsNullOrEmpty(iss.RelatedPackageId))
                DrawModMetaRow(listing, iss.RelatedPackageId, iss.RelatedPackageId);
            if (settings != null && !string.IsNullOrEmpty(iss.RelatedPackageId))
            {
                var row = listing.GetRect(26f);
                if (Widgets.ButtonText(new Rect(row.x + 152f, row.y, 100f, 24f), "ModCompatChecker.WebSearch".Translate()))
                {
                    var term = iss.RelatedPackageId ?? iss.ModName;
                    var url = "https://steamcommunity.com/workshop/browse/?appid=294100&searchtext="
                        + Uri.EscapeDataString(term);
                    try { Process.Start(url); }
                    catch { try { Application.OpenURL(url); } catch { } }
                }
            }
        }

        private static Color RiskColor(ConflictRisk r)
        {
            switch (r)
            {
                case ConflictRisk.High: return Color.red;
                case ConflictRisk.Medium: return new Color(1f, 0.7f, 0.2f);
                default: return new Color(0.5f, 0.8f, 0.5f);
            }
        }

        private static string RiskLabel(ConflictRisk r)
        {
            switch (r)
            {
                case ConflictRisk.High: return "ModCompatChecker.RiskHigh".Translate();
                case ConflictRisk.Medium: return "ModCompatChecker.RiskMedium".Translate();
                default: return "ModCompatChecker.RiskLow".Translate();
            }
        }

        private static string Truncate(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLen)
                return text;
            return text.Substring(0, maxLen) + "...";
        }

        // ── Error Analysis Section ──
        private enum ErrorSource { File, Runtime, Clipboard }
        private ErrorSource _errorSource = ErrorSource.Runtime;

        private readonly List<ErrorEntry> _errorEntries = new List<ErrorEntry>();
        private Vector2 _errorScroll = Vector2.zero;
        private int _selectedCount = 1;
        private int _customCount = 30;
        private bool _showInfo = true, _showWarnings = true, _showErrors = true;
        private int _presetCount = 10;
        private bool _showQuantityPicker;
        private static readonly int[] PresetCounts = { 1, 5, 10, 20 };

        // 每个数据源独立的字典
        private readonly Dictionary<ErrorSource, string> _errRes = new Dictionary<ErrorSource, string>();
        private readonly Dictionary<ErrorSource, string> _errCost = new Dictionary<ErrorSource, string>();
        private readonly Dictionary<ErrorSource, List<string>> _errDep = new Dictionary<ErrorSource, List<string>>();
        private readonly Dictionary<ErrorSource, string> _fupRes = new Dictionary<ErrorSource, string>();

        private string ErrorResult
        {
            get { _errRes.TryGetValue(_errorSource, out var v); return v ?? ""; }
            set { _errRes[_errorSource] = value; }
        }
        private string ErrorCost
        {
            get { _errCost.TryGetValue(_errorSource, out var v); return v ?? ""; }
            set { _errCost[_errorSource] = value; }
        }
        private List<string> ErrorDeps
        {
            get { _errDep.TryGetValue(_errorSource, out var v); return v ?? new List<string>(); }
            set { _errDep[_errorSource] = value; }
        }
        private string FollowUpResult
        {
            get { _fupRes.TryGetValue(_errorSource, out var v); return v ?? ""; }
            set { _fupRes[_errorSource] = value; }
        }

        private bool _isErrorAnalyzing, _errorCancelled;
        private string _clipboardOverride = ""; private Vector2 _clipScroll = Vector2.zero;
        private Vector2 _resultAreaScroll = Vector2.zero;
        private readonly Stopwatch _analysisTimer = new Stopwatch();

        private string _followUpQuestion = "";
        private bool _followUpOpen, _followUpRunning, _steamSearchOpen;
        private bool _needsRefresh = true; private string _quickAsk = "", _quickAnswer = ""; private bool _quickRunning; private Vector2 _quickScroll;


        // === Encyclopedia fields ===
        // === Update checker fields ===
        // === Relationship analyzer fields ===
        private string _relSelectedMod = null;
        private Core.ModRelationship _relationship;
        private bool _relOpen, _relDepends, _relDepended, _relHarmony, _relDef, _relOrder;
        private Vector2 _relScroll = Vector2.zero;

        private List<Core.ModVersionInfo> _updateVersions;
        private bool _showUpdates;
        private Vector2 _updateScroll = Vector2.zero;

        private bool _encyclopediaOpen;
        private Vector2 _encycloScroll = Vector2.zero;
        private List<(Core.ErrorEntry Entry, System.Text.RegularExpressions.Match Match)> _encycloMatches;

        private void DrawErrorSection(Listing_Standard listing, ModCompatSettings settings)
        {
            if (_needsRefresh)
            {
                _needsRefresh = false;
                RefreshErrorList();
            }
            bool hasAPI = settings.IsAIConfigured();

            var srcRect = listing.GetRect(26f);
            float sw = srcRect.width / 3f;
            DrawSourceTab(new Rect(srcRect.x, srcRect.y, sw, 24f), "ModCompatChecker.LogFile".Translate(), ErrorSource.File);
            DrawSourceTab(new Rect(srcRect.x + sw, srcRect.y, sw, 24f), "ModCompatChecker.RuntimeLog".Translate(), ErrorSource.Runtime);
            DrawSourceTab(new Rect(srcRect.x + sw * 2, srcRect.y, sw, 24f), "ModCompatChecker.Clipboard".Translate(), ErrorSource.Clipboard);
            listing.Gap(4f);

            if (_errorSource == ErrorSource.Runtime)
            {
                DrawLogFilters(listing);
                listing.Gap(4f);
            }

            if (_errorSource == ErrorSource.Clipboard)
            {
                var cbRect2 = listing.GetRect(72f); Widgets.BeginScrollView(cbRect2, ref _clipScroll, new Rect(0f, 0f, cbRect2.width - 16f, 120f)); _clipboardOverride = GUI.TextArea(new Rect(0f, 0f, cbRect2.width - 16f, 120f), _clipboardOverride); Widgets.EndScrollView();
                if (listing.ButtonText("ModCompatChecker.ReadClipboard".Translate()))
                    _clipboardOverride = GUIUtility.systemCopyBuffer ?? "";
                listing.Gap(4f);
            }

            if (_errorEntries.Count > 0 && _errorSource != ErrorSource.Clipboard)
            {
                var listRect = listing.GetRect(Mathf.Min(_errorEntries.Count * 22f, 180f));
                Widgets.BeginScrollView(listRect, ref _errorScroll,
                    new Rect(0f, 0f, listRect.width - 20f, _errorEntries.Count * 22f));
                var innerList = new Listing_Standard();
                innerList.Begin(new Rect(0f, 0f, listRect.width - 20f, _errorEntries.Count * 22f));
                for (int i = 0; i < _errorEntries.Count; i++)
                {
                    var e = _errorEntries[i];
                    GUI.color = e.Selected ? new Color(0.3f, 0.65f, 0.3f) : EntryColor(e.Level);
                    if (innerList.ButtonText("[" + e.Level + "] " + e.Time + "  " + Truncate(e.Brief, 70)))
                        e.Selected = !e.Selected;
                }
                GUI.color = Color.white;
                innerList.End();
                Widgets.EndScrollView();
                listing.Gap(4f);

                if (_errorSource != ErrorSource.Runtime)
                    DrawQuantityPicker(listing);
            }

            if (!string.IsNullOrEmpty(ErrorCost))
            {
                GUI.color = new Color(0.7f, 0.7f, 0.3f);
                listing.Label("ModCompatChecker.CostEstimate".Translate() + " " + ErrorCost, -1);
                GUI.color = Color.white;
            }

            if (hasAPI)
            {
                bool analyzing;
                lock (_lock) { analyzing = _isErrorAnalyzing; }
                if (analyzing)
                {
                    listing.Label("ModCompatChecker.AnalyzingSpace".Translate() + _analysisTimer.Elapsed.TotalSeconds.ToString("F1") + "s", -1);
                    if (listing.ButtonText("ModCompatChecker.CancelAnalysis".Translate()))
                        _errorCancelled = true;
                }
                else
                {
                    if (listing.ButtonText("ModCompatChecker.StartAIAnalysis".Translate()))
                        StartErrorAnalysis(settings);
                }
            }
            else
            {
                GUI.color = new Color(0.4f, 0.4f, 0.4f);
                listing.Label("ModCompatChecker.PleaseConfigAPI".Translate(), -1);
                GUI.color = Color.white;
            }

            listing.Gap(4f);

            if (!string.IsNullOrEmpty(ErrorResult))
            {
                var copyRow = listing.GetRect(24f);
                if (Widgets.ButtonText(new Rect(copyRow.x + copyRow.width - 80f, copyRow.y, 80f, 22f), "ModCompatChecker.CopyResult".Translate()))
                    GUIUtility.systemCopyBuffer = ErrorResult;

                var resRect = listing.GetRect(Mathf.Max(150f, 200f));
                Widgets.DrawBoxSolid(resRect, new Color(0.1f, 0.1f, 0.1f, 0.8f));
                var innerRes = new Rect(resRect.x + 6f, resRect.y + 4f, resRect.width - 24f, resRect.height - 8f);
                float textH = Text.CalcHeight(ErrorResult, innerRes.width);
                var viewRes = new Rect(0f, 0f, innerRes.width - 4f, textH);
                Widgets.BeginScrollView(innerRes, ref _resultAreaScroll, viewRes);
                GUI.color = new Color(0.85f, 0.85f, 0.85f);
                Widgets.Label(new Rect(0f, 0f, innerRes.width - 4f, textH), ErrorResult);
                GUI.color = Color.white;
                Widgets.EndScrollView();
            }

            if (!string.IsNullOrEmpty(ErrorResult))
            {
                ResultRenderer.DrawCollapsibleSection(listing, ref _followUpOpen, "ModCompatChecker.FollowUpAnalysis".Translate(), 100f, () =>
                {
                    _followUpQuestion = listing.TextEntry(_followUpQuestion, 2);
                    listing.Gap(2f);
                    bool running;
                    lock (_lock) { running = _followUpRunning; }
                    if (running)
                        listing.Label("ModCompatChecker.Analyzing".Translate(), -1);
                    else if (!hasAPI)
                    {
                        GUI.color = new Color(0.4f, 0.4f, 0.4f);
                        listing.Label("ModCompatChecker.NeedAPIKey".Translate(), -1);
                        GUI.color = Color.white;
                    }
                    else if (listing.ButtonText("ModCompatChecker.SendFollowUp".Translate()))
                        StartFollowUp(settings);
                    if (!string.IsNullOrEmpty(FollowUpResult))
                    {
                        listing.Gap(4f);
                        var ar = listing.GetRect(60f);
                        Widgets.DrawBoxSolid(ar, new Color(0.08f, 0.12f, 0.08f));
                        float fupH = Text.CalcHeight(FollowUpResult, ar.width - 8f);
                        Widgets.Label(new Rect(ar.x + 4f, ar.y + 2f, ar.width - 8f, fupH), FollowUpResult);
                    }
                });
            }

            var deps = ErrorDeps;
            if (!string.IsNullOrEmpty(ErrorResult) && deps.Count > 0)
            {
                ResultRenderer.DrawCollapsibleSection(listing, ref _steamSearchOpen,
                    "ModCompatChecker.SearchRequiredMod".Translate() + " (" + deps.Count + ")", deps.Count * 34f + 20f, () =>
                {
                    listing.Label("ModCompatChecker.DetectedMissingMods".Translate(), -1);
                    foreach (var dep in deps)
                    {
                        var cr = listing.GetRect(28f);
                        Widgets.DrawBoxSolid(cr, new Color(0.12f, 0.18f, 0.28f));
                        Widgets.Label(new Rect(cr.x + 8f, cr.y + 4f, cr.width * 0.55f, 20f), "[MOD] " + dep);
                        if (Widgets.ButtonText(new Rect(cr.x + cr.width * 0.6f, cr.y + 2f, cr.width * 0.38f, 24f),
                            "ModCompatChecker.ViewOnSteam".Translate()))
                        {
                            var url = "https://steamcommunity.com/workshop/browse/?appid=294100&searchtext="
                                + Uri.EscapeDataString(dep);
                            try { Process.Start(url); }
                            catch { try { Application.OpenURL(url); } catch { } }
                        }
                    }
                });
            }
        }

        private void DrawSourceTab(Rect rect, string label, ErrorSource source)
        {
            GUI.color = _errorSource == source
                ? new Color(0.3f, 0.55f, 0.3f)
                : new Color(0.2f, 0.2f, 0.2f);
            Widgets.DrawBoxSolid(rect, GUI.color);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, label);
            Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonInvisible(rect))
            {
                _errorSource = source;
                _needsRefresh = true;
            }
        }

        private void DrawLogFilters(Listing_Standard listing)
        {
            bool changed = false;
            var filterRect = listing.GetRect(24f);
            float cw = Math.Max(80f, (filterRect.width - 16f) / 3f);
            if (DrawFilterCb(filterRect.x, filterRect.y, cw, "Info", ref _showInfo, new Color(0.7f, 0.7f, 0.7f)))
                changed = true;
            if (DrawFilterCb(filterRect.x + cw, filterRect.y, cw, "Warning", ref _showWarnings, new Color(0.9f, 0.75f, 0.3f)))
                changed = true;
            if (DrawFilterCb(filterRect.x + cw * 2, filterRect.y, cw, "Error", ref _showErrors, new Color(0.95f, 0.35f, 0.35f)))
                changed = true;

            listing.Gap(4f);
            var qr = listing.GetRect(26f);
            Widgets.Label(new Rect(qr.x, qr.y + 3f, 55f, 20f), "ModCompatChecker.Quantity".Translate());
            string qLabel = _presetCount > 0 ? _presetCount.ToString() : (_customCount.ToString() + "+");
            if (Widgets.ButtonText(new Rect(qr.x + 55f, qr.y, 70f, 24f), qLabel))
                _showQuantityPicker = !_showQuantityPicker;

            if (_showQuantityPicker)
            {
                listing.Gap(2f);
                var pr = listing.GetRect(22f);
                float px = pr.x;
                foreach (var p in PresetCounts)
                {
                    if (Widgets.ButtonText(new Rect(px, pr.y, 42f, 20f), p.ToString()))
                    {
                        _presetCount = p;
                        _showQuantityPicker = false;
                        changed = true;
                    }
                    px += 46f;
                }
                if (Widgets.ButtonText(new Rect(px, pr.y, 60f, 20f), "ModCompatChecker.Custom".Translate()))
                {
                    _presetCount = 0;
                    _showQuantityPicker = false;
                    changed = true;
                }
                if (_presetCount == 0)
                {
                    listing.Gap(2f);
                    listing.Label("ModCompatChecker.CustomQuantity".Translate(), -1);
                    var cs = listing.TextEntry(_customCount.ToString());
                    if (int.TryParse(cs, out var cq) && cq >= 1 && cq <= 200)
                    {
                        _customCount = cq;
                        changed = true;
                    }
                }
            }
            if (changed) _needsRefresh = true;
        }

        private bool DrawFilterCb(float x, float y, float w, string label, ref bool val, Color color)
        {
            var rect = new Rect(x, y, w, 22f);
            GUI.color = val ? color : new Color(0.3f, 0.3f, 0.3f);
            Widgets.DrawBoxSolid(new Rect(x, y + 4f, 14f, 14f), GUI.color);
            GUI.color = Color.white;
            Widgets.Label(new Rect(x + 18f, y + 2f, w - 20f, 18f), label);
            if (Widgets.ButtonInvisible(rect))
            {
                val = !val;
                return true;
            }
            return false;
        }

        private void DrawQuantityPicker(Listing_Standard listing)
        {
            var cr = listing.GetRect(24f);
            Widgets.Label(new Rect(cr.x, cr.y + 2f, 40f, 20f), "ModCompatChecker.Quantity".Translate());
            int q;
            lock (_lock) { q = _selectedCount; }
            var ts = listing.TextEntry(q.ToString());
            if (int.TryParse(ts, out var qv) && qv >= 1 && qv <= _errorEntries.Count)
                lock (_lock) { _selectedCount = qv; }
        }

        private static Color EntryColor(string level)
        {
            switch (level)
            {
                case "ERR": return new Color(0.9f, 0.35f, 0.35f);
                case "WRN": return new Color(0.9f, 0.75f, 0.3f);
                default: return new Color(0.6f, 0.6f, 0.6f);
            }
        }
        // ── Thread: Scan ──
        private void StartScan()
        {
            lock (_lock)
            {
                if (_isScanning) return;
                _isScanning = true;
                _aiResults.Clear();
                _pendingAI.Clear();
                _batchStatus = "";
                _cachedAI.Clear();
            }
            new Thread(() =>
            {
                try
                {
                    var report = ConflictDetector.RunFullScan();
                    lock (_lock) { _report = report; _hasScanned = true; _isScanning = false; _cachedReport = report; _cachedHasScanned = true; } SaveScanCache(report);
                }
                catch (Exception ex)
                {
                    Log.Error("[MC] Scan: " + ex.Message);
                    lock (_lock) { _isScanning = false; }
                }
            }) { IsBackground = true }.Start();
        }

        // ── Thread: Single AI Analysis ──
        private void StartSingleAnalysis(int key, ModCompatSettings settings)
        {
            var mi = settings.GetSelectedModelInfo();
            var ep = settings.GetEndpointAndProvider(mi);
            var mid = settings.IsCustomModel() ? settings.CustomModelId : mi.Id;
            ConflictReport report;
            lock (_lock)
            {
                report = _report;
                if (report == null) return;
                if (_pendingAI.Contains(key)) return;
                _pendingAI.Add(key);
            }
            new Thread(() =>
            {
                try
                {
                    string result;
                    if (key >= 2000 && key - 2000 < report.DependencyIssues.Count)
                        result = AIService.AnalyzeDependencyIssue(
                            report.DependencyIssues[key - 2000], ep.endpoint, settings.APIKey, mid, ep.provider);
                    else if (key < 1000 && key < report.HarmonyConflicts.Count)
                        result = AIService.AnalyzeHarmonyConflict(
                            report.HarmonyConflicts[key], ep.endpoint, settings.APIKey, mid, ep.provider);
                    else if (key >= 1000 && key < 2000 && key - 1000 < report.DefConflicts.Count)
                        result = AIService.AnalyzeDefConflict(
                            report.DefConflicts[key - 1000], ep.endpoint, settings.APIKey, mid, ep.provider);
                    else
                        result = "ModCompatChecker.InvalidRequest".Translate();
                    if (!_disposed) lock (_lock) { _aiResults[key] = result; _pendingAI.Remove(key); _cachedAI[key] = result; }
                }
                catch (Exception ex)
                {
                    if (!_disposed) lock (_lock) { var em = "ModCompatChecker.ErrorPrefix".Translate() + ex.Message; _aiResults[key] = em; _pendingAI.Remove(key); _cachedAI[key] = em; }
                }
            }) { IsBackground = true }.Start();
        }

        // ── Thread: Batch AI Analysis ──
        private void StartBatchAnalysis(ModCompatSettings settings)
        {
            lock (_lock)
            {
                if (_isBatchAnalyzing) return;
                _isBatchAnalyzing = true;
                _aiResults.Clear();
                _pendingAI.Clear();
                _cachedAI.Clear();
            }
            var mi = settings.GetSelectedModelInfo();
            var ep = settings.GetEndpointAndProvider(mi);
            var mid = settings.IsCustomModel() ? settings.CustomModelId : mi.Id;
            ConflictReport report;
            lock (_lock) { report = _report; if (report == null) { _isBatchAnalyzing = false; return; } }
            new Thread(() =>
            {
                int total = report.HarmonyConflicts.Count + report.DefConflicts.Count + report.DependencyIssues.Count;
                int done = 0;
                void UpdateStatus() { if (!_disposed) lock (_lock) { _batchStatus = "ModCompatChecker.AnalyzingSpace".Translate() + done + "/" + total; } }

                for (int i = 0; i < report.HarmonyConflicts.Count; i++)
                {
                    if (_disposed) break;
                    try
                    {
                        var res = AIService.AnalyzeHarmonyConflict(report.HarmonyConflicts[i],
                            ep.endpoint, settings.APIKey, mid, ep.provider);
                        if (!_disposed) lock (_lock) { _aiResults[i] = res; _cachedAI[i] = res; }
                    }
                    catch (Exception ex) { if (!_disposed) lock (_lock) { var em = "ModCompatChecker.ErrorPrefix".Translate() + ex.Message; _aiResults[i] = em; _cachedAI[i] = em; } }
                    if (_disposed) break;
                    done++; UpdateStatus();
                }
                for (int i = 0; i < report.DefConflicts.Count; i++)
                {
                    if (_disposed) break;
                    try
                    {
                        var res = AIService.AnalyzeDefConflict(report.DefConflicts[i],
                            ep.endpoint, settings.APIKey, mid, ep.provider);
                        if (!_disposed) lock (_lock) { _aiResults[i + 1000] = res; _cachedAI[i + 1000] = res; }
                    }
                    catch (Exception ex) { if (!_disposed) lock (_lock) { var em = "ModCompatChecker.ErrorPrefix".Translate() + ex.Message; _aiResults[i + 1000] = em; _cachedAI[i + 1000] = em; } }
                    if (_disposed) break;
                    done++; UpdateStatus();
                }
                for (int i = 0; i < report.DependencyIssues.Count; i++)
                {
                    if (_disposed) break;
                    try
                    {
                        var res = AIService.AnalyzeDependencyIssue(report.DependencyIssues[i],
                            ep.endpoint, settings.APIKey, mid, ep.provider);
                        if (!_disposed) lock (_lock) { _aiResults[i + 2000] = res; _cachedAI[i + 2000] = res; }
                    }
                    catch (Exception ex) { if (!_disposed) lock (_lock) { var em = "ModCompatChecker.ErrorPrefix".Translate() + ex.Message; _aiResults[i + 2000] = em; _cachedAI[i + 2000] = em; } }
                    if (_disposed) break;
                    done++; UpdateStatus();
                }
                if (!_disposed) lock (_lock) { _isBatchAnalyzing = false; _batchStatus = "ModCompatChecker.Complete".Translate() + " (" + total + " " + "ModCompatChecker.Items".Translate() + ")"; }
            }) { IsBackground = true }.Start();
        }

        // ── Thread: Error Analysis ──
        private void StartErrorAnalysis(ModCompatSettings settings)
        {
            lock (_lock) { if (_isErrorAnalyzing) return; _isErrorAnalyzing = true; }
            _errorCancelled = false;
            ErrorResult = "";
            FollowUpResult = "";
            ErrorDeps = new List<string>();
            _followUpOpen = false;
            _steamSearchOpen = false;
            _analysisTimer.Restart();

            string errorText;
            if (_errorSource == ErrorSource.Clipboard)
            {
                errorText = string.IsNullOrEmpty(_clipboardOverride)
                    ? GUIUtility.systemCopyBuffer ?? ""
                    : _clipboardOverride;
                if (string.IsNullOrEmpty(errorText))
                {
                    ErrorResult = "ModCompatChecker.ClipboardEmpty".Translate();
                    lock (_lock) { _isErrorAnalyzing = false; }
                    return;
                }
            }
            else
            {
                var selected = new List<string>();
                int take = Math.Min(_selectedCount, _errorEntries.Count);
                for (int i = Math.Max(0, _errorEntries.Count - take); i < _errorEntries.Count; i++)
                    selected.Add("[" + _errorEntries[i].Level + " " + _errorEntries[i].Time + "] " + _errorEntries[i].Full);
                errorText = string.Join("\n---\n", selected);
            }

            var mi = settings.GetSelectedModelInfo();
            ErrorCost = CostEstimator.FormatCost(CostEstimator.Estimate(errorText, mi.Id, 300));
            var ep = settings.GetEndpointAndProvider(mi);
            var mid = settings.IsCustomModel() ? settings.CustomModelId : mi.Id;

            new Thread(() =>
            {
                try
                {
                    var prompt = PromptBuilder.BuildErrorAnalysisPrompt(errorText, new ConflictReport(), PromptBuilder.GetPromptLanguage());
                    var result = AIService.CallAPIWithTimeout(ep.endpoint, settings.APIKey, mid, prompt,
                        ep.provider, settings.AnalysisTimeoutSeconds, ref _errorCancelled);
                    _analysisTimer.Stop();
                    if (!_disposed) lock (_lock)
                    {
                        ErrorResult = _errorCancelled ? "ModCompatChecker.AnalysisCancelledNL".Translate() + result : result;
                        if (!_disposed) lock (_lock) { try { _encycloMatches = Core.ErrorEncyclopedia.MatchError(result); } catch { _encycloMatches = null; } }
                        if (_analysisTimer.Elapsed.TotalSeconds > settings.AnalysisTimeoutSeconds * 0.8)
                            ErrorResult += "\n\n耗时 " + _analysisTimer.Elapsed.TotalSeconds.ToString("F1") + "s";
                        ErrorDeps = DependencyExtractor.Extract(errorText + "\n" + ErrorResult); _cachedErrorResult[_errorSource] = ErrorResult; _cachedDeps[_errorSource] = new List<string>(ErrorDeps);
                    }
                }
                catch (Exception ex)
                {
                    if (!_disposed) lock (_lock) { ErrorResult = "ModCompatChecker.FailedPrefix".Translate() + ex.Message; }
                    _analysisTimer.Stop();
                }
                if (!_disposed) lock (_lock) { _isErrorAnalyzing = false; }
            }) { IsBackground = true }.Start();
        }

        // ── Thread: 追问分析 Analysis ──
        private void StartFollowUp(ModCompatSettings settings)
        {
            if (string.IsNullOrEmpty(_followUpQuestion) || !settings.IsAIConfigured()) return;
            lock (_lock) { if (_followUpRunning) return; _followUpRunning = true; }
            FollowUpResult = "";
            var mi = settings.GetSelectedModelInfo();
            var ep = settings.GetEndpointAndProvider(mi);
            var mid = settings.IsCustomModel() ? settings.CustomModelId : mi.Id;
            var prompt = "ModCompatChecker.PreviousAnalysis".Translate() + ErrorResult + "\n\n" + "ModCompatChecker.FollowUpQuestion".Translate() + _followUpQuestion + "\n\n" + "ModCompatChecker.ConciselyAnswer".Translate();
            bool cancel = false;
            new Thread(() =>
            {
                try
                {
                    var response = AIService.CallAPIWithTimeout(ep.endpoint, settings.APIKey, mid, prompt,
                        ep.provider, 15, ref cancel);
                    if (!_disposed) lock (_lock) { FollowUpResult = response; }
                }
                catch (Exception ex)
                {
                    if (!_disposed) lock (_lock) { FollowUpResult = "ModCompatChecker.FailedPrefix".Translate() + ex.Message; }
                }
                if (!_disposed) lock (_lock) { _followUpRunning = false; }
            }) { IsBackground = true }.Start();
        }

        // ── Refresh Error List ──
        private void RefreshErrorList()
        {
            _errorEntries.Clear();
            _followUpOpen = false;
            _steamSearchOpen = false;
            _selectedCount = 1;

            if (_errorSource == ErrorSource.Runtime)
            {
                int count = _presetCount > 0 ? _presetCount : _customCount;
                var entries = LogCapture.GetRecent(count, _showInfo, _showWarnings, _showErrors);
                foreach (var e in entries)
                    _errorEntries.Add(new ErrorEntry
                    {
                        Level = e.Level == LogCapture.LogLevel.Error ? "ERR"
                            : (e.Level == LogCapture.LogLevel.Warning ? "WRN" : "INF"),
                        Time = e.Timestamp,
                        Brief = Truncate(e.Message, 110),
                        Full = e.Message
                    });
            }
            else if (_errorSource == ErrorSource.File)
            {
                LoadLogFile();
            }
            _selectedCount = Math.Min(_selectedCount, _errorEntries.Count); _cachedErrors.Clear(); _cachedErrors.AddRange(_errorEntries);
        }

        private static readonly Regex TimestampRegex = new Regex(@"\[(\d{2}:\d{2}:\d{2})\]", RegexOptions.Compiled);

        private void LoadLogFile()
        {
            try
            {
                var path = Path.Combine(Application.dataPath, "..", "Player.log");
                if (!File.Exists(path))
                    path = Path.Combine(Application.dataPath, "..", "Player-prev.log");
                if (!File.Exists(path)) return;
                var lines = File.ReadAllLines(path);
                int start = Math.Max(0, lines.Length - 1000);
                int cnt = 0;
                for (int i = lines.Length - 1; i >= start; i--)
                {
                    var line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line)
                        || (!line.Contains("Exception:") && !line.StartsWith("Error in") && !line.Contains("Error")))
                        continue;
                    var stack = new List<string>();
                    for (int j = i; j < lines.Length && j < i + 12; j++)
                        stack.Add(lines[j]);
                    var tsMatch = TimestampRegex.Match(line);
                    _errorEntries.Add(new ErrorEntry
                    {
                        Level = "ERR",
                        Time = tsMatch.Success ? tsMatch.Groups[1].Value : "??:??",
                        Brief = line.Length > 110 ? line.Substring(0, 110) : line,
                        Full = string.Join("\n", stack)
                    });
                    cnt++;
                    if (cnt >= 50) break;
                }
                _errorEntries.Reverse();
            }
            catch { /* ignore */ }
        }

        private void StartQuickAsk(ModCompatSettings settings)
        {
            var q = _quickAsk.Trim();
            if (string.IsNullOrEmpty(q) || !settings.IsAIConfigured()) return;
            lock (_lock) { if (_quickRunning) return; _quickRunning = true; }
            _quickAnswer = ""; _quickScroll = Vector2.zero;
            var mi = settings.GetSelectedModelInfo();
            var ep = settings.GetEndpointAndProvider(mi);
            var mid = settings.IsCustomModel() ? settings.CustomModelId : mi.Id;
            // Build context from current state
            var ctx = "";
            ConflictReport rep; lock (_lock) { rep = _report; }
            if (rep != null && _hasScanned)
                ctx = "ModCompatChecker.CurrentScanResult".Translate() + rep.TotalLoadedMods + " MOD, " + rep.TotalConflictCount + " " + "ModCompatChecker.ProblemCount".Translate() + "。\n";
            var errResult = ErrorResult;
            if (!string.IsNullOrEmpty(errResult))
                ctx += "ModCompatChecker.RecentErrorAnalysis".Translate() + errResult.Substring(0, Math.Min(800, errResult.Length)) + "\n";
            var prompt = ctx + "ModCompatChecker.UserQuestion".Translate() + q + "\n\n" + "ModCompatChecker.QuickAskPrompt".Translate();
            bool cancel = false;
            new Thread(() =>
            {
                try
                {
                    var response = AIService.CallAPIWithTimeout(ep.endpoint, settings.APIKey, mid, prompt,
                        ep.provider, Math.Min(30, settings.AnalysisTimeoutSeconds), ref cancel);
                    if (!_disposed) lock (_lock) { _quickAnswer = response; _quickScroll = Vector2.zero; }
                }
                catch (Exception ex)
                {
                    if (!_disposed) lock (_lock) { _quickAnswer = "ModCompatChecker.FailedPrefix".Translate() + ex.Message; _quickScroll = Vector2.zero; }
                }
                if (!_disposed) lock (_lock) { _quickRunning = false; }
            }) { IsBackground = true }.Start();
        }

        private static string GetFirstLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            int idx = text.IndexOf('\n');
            if (idx > 0) return text.Substring(0, idx).TrimEnd('\r');
            if (text.Length > 80) return text.Substring(0, 80) + "...";
            return text;
        }

        private class ErrorEntry
        {
            public string Level, Time, Brief, Full;
            public bool Selected;
        }

        private void DrawRelSection(string title, ref bool open, List<string> items, Color col, ref float y, float width)
        {
            if (items == null || items.Count == 0) return;
            var headerRect = new Rect(0f, y, width, 20f);
            GUI.color = col;
            string arrow = open ? "v" : ">";
            Widgets.Label(headerRect, arrow + " " + title + " (" + items.Count + ")");
            GUI.color = Color.white;
            if (Widgets.ButtonInvisible(headerRect)) open = !open;
            y += 22f;
            if (open)
            {
                foreach (var item in items)
                {
                    Widgets.Label(new Rect(16f, y, width - 20f, 18f), item);
                    y += 20f;
                }
            }
        }
    }
}