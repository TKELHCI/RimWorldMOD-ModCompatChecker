using System;
using System.Collections.Generic;
using System.Threading;
using ModCompatChecker.AI;
using ModCompatChecker.Core;
using UnityEngine;
using Verse;

namespace ModCompatChecker.UI
{
    /// <summary>
    /// Shared UI component used by both Mod Settings page and Landing window.
    /// Eliminates code duplication for model selection, API config, and connection testing.
    /// </summary>
    public static class SharedSettingsUI
    {
        public class UIState
        {
            public bool ShowModelSelector;
            public string TestResult = "";
            public bool ShowTestResult;
            public bool IsTesting;
            public readonly object Lock = new object();
            public bool Disposed;
        }

        /// <summary>
        /// Draw collapsible model selector. Returns the UI state for progress tracking.
        /// </summary>
        private static Dictionary<AI.ModelConfig.ApiProvider, bool> _providerExpanded = new Dictionary<AI.ModelConfig.ApiProvider, bool>();

        public static void DrawModelSelector(Listing_Standard listing, ModCompatSettings settings, UIState state)
        {
            var modelInfo = settings.GetSelectedModelInfo();
            string currentName = settings.IsCustomModel()
                ? "ModCompatChecker.CustomModel".Translate() + ": {" + (string.IsNullOrEmpty(settings.CustomModelId) ? "ModCompatChecker.NotSet".Translate().ToString() : settings.CustomModelId) + "}"
                : modelInfo.DisplayKey.Translate().ToString();

            var headerRect = listing.GetRect(30f);
            GUI.color = state.ShowModelSelector
                ? new Color(0.25f, 0.5f, 0.25f)
                : new Color(0.15f, 0.35f, 0.15f);
            Widgets.DrawBoxSolid(headerRect, GUI.color);
            GUI.color = Color.white;

            string arrow = state.ShowModelSelector ? "v" : ">";
            Widgets.Label(new Rect(headerRect.x + 10f, headerRect.y + 5f,
                headerRect.width - 20f, 20f), arrow + " " + "ModCompatChecker.CurrentModel".Translate() + " " + currentName);

            if (Widgets.ButtonInvisible(headerRect))
                state.ShowModelSelector = !state.ShowModelSelector;

            if (state.ShowModelSelector)
            {
                listing.Gap(2f);
                int globalIdx = 0;

                foreach (var provider in AI.ModelConfig.GetProviderOrder())
                {
                    var providerModels = new List<AI.ModelConfig.ModelInfo>();
                    foreach (var m in AI.ModelConfig.Models)
                        if (m.Provider == provider) providerModels.Add(m);
                    if (providerModels.Count == 0) continue;

                    // Ensure expand state exists
                    if (!_providerExpanded.ContainsKey(provider))
                        _providerExpanded[provider] = false;

                    // Collapsible provider header
                    var provRect = listing.GetRect(26f);
                    string provArrow = _providerExpanded[provider] ? "v" : ">";
                    GUI.color = _providerExpanded[provider]
                        ? new Color(0.35f, 0.55f, 0.8f)
                        : new Color(0.25f, 0.4f, 0.6f);
                    Widgets.DrawBoxSolid(new Rect(provRect.x + 4f, provRect.y + 2f, provRect.width - 8f, 22f), GUI.color);
                    GUI.color = Color.white;
                    Widgets.Label(new Rect(provRect.x + 10f, provRect.y + 3f, provRect.width - 20f, 20f),
                        provArrow + " " + AI.ModelConfig.GetProviderGroupName(provider) + " (" + providerModels.Count + ")");

                    if (Widgets.ButtonInvisible(provRect))
                        _providerExpanded[provider] = !_providerExpanded[provider];

                    if (_providerExpanded[provider])
                    {
                        foreach (var model in providerModels)
                        {
                            var rowRect = listing.GetRect(22f);
                            bool selected = !settings.IsCustomModel() && settings.SelectedModelIndex == globalIdx;
                            string prefix = selected ? "O" : "o";
                            GUI.color = selected
                                ? new Color(0.3f, 0.7f, 0.3f)
                                : new Color(0.5f, 0.5f, 0.5f);

                            if (Widgets.ButtonText(new Rect(rowRect.x + 22f, rowRect.y, rowRect.width - 26f, 20f),
                                prefix + "  " + model.DisplayKey.Translate().ToString()))
                            {
                                settings.SelectedModelIndex = globalIdx;
                                settings.APIEndpoint = model.DefaultEndpoint;
                            }
                            GUI.color = Color.white;
                            globalIdx++;
                        }
                    }
                    else
                    {
                        // Skip globalIdx for collapsed models
                        globalIdx += providerModels.Count;
                    }
                }

                // Custom model option
                listing.Gap(4f);
                var customRect = listing.GetRect(22f);
                bool customSelected = settings.IsCustomModel();
                GUI.color = customSelected
                    ? new Color(0.3f, 0.7f, 0.3f)
                    : new Color(0.5f, 0.5f, 0.5f);
                if (Widgets.ButtonText(new Rect(customRect.x + 22f, customRect.y, customRect.width - 26f, 20f),
                    (customSelected ? "O" : "o") + "  " + "ModCompatChecker.CustomModel".Translate()))
                {
                    settings.SelectedModelIndex = AI.ModelConfig.Models.Count;
                }
                GUI.color = Color.white;
                listing.Gap(4f);
            }
        }

        /// <summary>
        /// Draw API settings fields (endpoint, key, timeout, test button).
        /// </summary>
        public static void DrawAPISettings(Listing_Standard listing, ModCompatSettings settings, UIState state)
        {
            listing.Label("ModCompatChecker.APISettingsLabel".Translate(), -1);

            if (state.ShowModelSelector && settings.SelectedModelIndex >= ModelConfig.Models.Count)
            {
                listing.Label("ModCompatChecker.CustomModelId".Translate(), -1);
                settings.CustomModelId = listing.TextEntry(settings.CustomModelId);
            }

            listing.Label("ModCompatChecker.APIEndpoint".Translate(), -1);
            settings.APIEndpoint = listing.TextEntry(settings.APIEndpoint);
            listing.Gap(3f);

            listing.Label("ModCompatChecker.APIKey".Translate(), -1);
            var prevKey = settings.APIKey;
            var newKey = listing.TextEntry(settings.APIKey);
            if (newKey != prevKey) { settings.APIKey = newKey; try { ModCompatChecker.ModCompatMod.Instance?.WriteSettings(); } catch { } }
            else settings.APIKey = newKey;
            listing.Gap(2f);
            // Privacy / data flow notice
            GUI.color = new Color(0.65f, 0.65f, 0.45f);
            Widgets.Label(listing.GetRect(36f), "ModCompatChecker.APIDataNotice".Translate());
            GUI.color = Color.white;
            listing.Gap(3f);

            bool _oldEnTimeout = settings.EnableAnalysisTimeout;
            Widgets.CheckboxLabeled(listing.GetRect(24f), "ModCompatChecker.EnableAnalysisTimeout".Translate(), ref settings.EnableAnalysisTimeout);
            if (_oldEnTimeout != settings.EnableAnalysisTimeout) ModCompatChecker.ModCompatMod.Instance.WriteSettings();
            listing.Gap(2f);
            if (!settings.EnableAnalysisTimeout) { GUI.color = new Color(0.4f, 0.4f, 0.4f); }
            listing.Label("ModCompatChecker.AnalysisTimeout".Translate() + settings.AnalysisTimeoutSeconds + "ModCompatChecker.SecondsUnit".Translate(), -1);
            var tsStr = listing.TextEntry(settings.AnalysisTimeoutSeconds.ToString());
            if (int.TryParse(tsStr, out var ts) && ts >= 5 && ts <= 120)
                settings.AnalysisTimeoutSeconds = ts;
            if (!settings.EnableAnalysisTimeout) { GUI.color = Color.white; }
            listing.Gap(2f);
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            Widgets.Label(listing.GetRect(36f), "ModCompatChecker.TimeoutHint".Translate());
            GUI.color = Color.white;
        }

        /// <summary>
        /// Draw test connection button and result.
        /// </summary>
        public static void DrawTestConnection(Listing_Standard listing, ModCompatSettings settings, UIState state)
        {
            var testRect = listing.GetRect(28f);
            bool testing;
            lock (state.Lock) { testing = state.IsTesting; }

            if (testing)
            {
                Widgets.Label(new Rect(testRect.x, testRect.y + 4f, testRect.width, 20f), "ModCompatChecker.Testing".Translate());
            }
            else
            {
                if (Widgets.ButtonText(new Rect(testRect.x, testRect.y, 110f, 26f), "ModCompatChecker.TestConnection".Translate()))
                    StartTestConnection(settings, state);
            }

            string testResult;
            lock (state.Lock) { testResult = state.TestResult; }
            if (state.ShowTestResult && !string.IsNullOrEmpty(testResult))
            {
                listing.Gap(2f);
                bool success = testResult.Contains("成功") || testResult.Contains("OK");
                GUI.color = success ? new Color(0.4f, 0.85f, 0.4f) : new Color(0.95f, 0.4f, 0.4f);
                listing.Label(testResult, -1);
                GUI.color = Color.white;
            }
        }

        /// <summary>
        /// Start API connection test in background thread.
        /// </summary>
        public static void StartTestConnection(ModCompatSettings settings, UIState state)
        {
            if (string.IsNullOrEmpty(settings.APIKey))
            {
                state.TestResult = "ModCompatChecker.EnterAPIKey".Translate();
                state.ShowTestResult = true;
                return;
            }

            lock (state.Lock) { if (state.IsTesting) return; state.IsTesting = true; }
            var _testLog = ApiLogMonitor.LogStart("测试连接");
            state.TestResult = "";
            state.ShowTestResult = true;

            Verse.Log.Message("[MC DEBUG] DrawModelSelector called, Models.Count=" + AI.ModelConfig.Models.Count + " Providers=" + AI.ModelConfig.GetProviderOrder().Count);
            var modelInfo = settings.GetSelectedModelInfo();
            var ep = settings.GetEndpointAndProvider(modelInfo);
            var apiKey = settings.APIKey;
            var endpoint = ep.endpoint;
            var provider = ep.provider;
            var modelId = settings.IsCustomModel() ? settings.CustomModelId : modelInfo.Id;

            var thread = new Thread(() =>
            {
                try
                {                    var result = AIService.TestConnection(endpoint, apiKey, modelId, provider, (settings.EnableAnalysisTimeout ? settings.AnalysisTimeoutSeconds : 99999));
                    if (!state.Disposed) lock (state.Lock)
                    {
                        state.TestResult = (result.Contains("OK") || result.Contains("Connection OK") || result.Length < 50)
                            ? "ModCompatChecker.ConnectionSuccess".Translate() : "ModCompatChecker.ConnectionFailed".Translate() + result;
                    }
                    if (!state.Disposed) ApiLogMonitor.LogComplete(_testLog, result.Substring(0, Math.Min(80, result.Length)));
                }
                catch (Exception ex)
                {
                    if (!state.Disposed) lock (state.Lock) { state.TestResult = "ModCompatChecker.ConnectionFailed".Translate() + ex.Message; }
                    if (!state.Disposed) ApiLogMonitor.LogFailed(_testLog, ex.Message);
                }
                if (!state.Disposed) lock (state.Lock) { state.IsTesting = false; }
                if (!state.Disposed) state.ShowTestResult = true;
            }) { IsBackground = true };
            thread.Start();
        }

        /// <summary>
        /// Draw collapsible API monitor panel showing recent API calls and force-stop button.
        /// </summary>
        
        /// <summary>
        /// Draw collapsible API balance checker section.
        /// </summary>
        public static void DrawBalanceCheck(Listing_Standard listing, ModCompatSettings settings, ref bool showBalance, ref Vector2 balanceScroll)
        {
            var headerRect = listing.GetRect(30f);
            string balanceText = "ModCompatChecker.BalanceCheck".Translate();
            if (ApiBalanceChecker.LastBalance >= 0)
                balanceText += " [" + ApiBalanceChecker.LastBalance.ToString("F2") + " " + ApiBalanceChecker.LastCurrency + "]";

            GUI.color = showBalance
                ? new Color(0.22f, 0.35f, 0.22f)
                : new Color(0.14f, 0.22f, 0.14f);
            Widgets.DrawBoxSolid(headerRect, GUI.color);
            GUI.color = Color.white;

            string arrow = showBalance ? "v" : ">";
            Widgets.Label(new Rect(headerRect.x + 10f, headerRect.y + 5f, headerRect.width - 20f, 20f), arrow + " " + balanceText);

            if (Widgets.ButtonInvisible(headerRect))
                showBalance = !showBalance;

            if (showBalance)
            {
                listing.Gap(4f);

                var togRect = listing.GetRect(24f);
                bool _oldBal = settings.EnableBalanceCheck;
            Widgets.CheckboxLabeled(togRect, "ModCompatChecker.EnableBalanceCheck".Translate(), ref settings.EnableBalanceCheck);
            if (_oldBal != settings.EnableBalanceCheck) ModCompatChecker.ModCompatMod.Instance.WriteSettings();

                listing.Gap(2f);

                listing.Label("ModCompatChecker.BalanceWarningThreshold".Translate() + " " + settings.BalanceWarningThreshold.ToString("F1"), -1);
                var thrStr = listing.TextEntry(settings.BalanceWarningThreshold.ToString("F1"));
                if (float.TryParse(thrStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var thr) && thr >= 0)
                    settings.BalanceWarningThreshold = thr;

                listing.Gap(4f);

                bool checking = ApiBalanceChecker.IsChecking;
                if (checking)
                {
                    listing.Label("ModCompatChecker.CheckingBalance".Translate(), -1);
                }
                else
                {
                    if (listing.ButtonText("ModCompatChecker.CheckBalanceNow".Translate()))
                        ApiBalanceChecker.CheckBalance(settings.APIEndpoint, settings.APIKey);
                }

                if (ApiBalanceChecker.LastCheckTime > DateTime.MinValue)
                {
                    listing.Gap(2f);
                    string timeStr = "ModCompatChecker.LastCheck".Translate() + ApiBalanceChecker.LastCheckTime.ToString("HH:mm:ss");
                    listing.Label(timeStr, -1);

                    if (ApiBalanceChecker.LastBalance >= 0)
                    {
                        float bal = ApiBalanceChecker.LastBalance;
                        GUI.color = bal <= settings.BalanceWarningThreshold
                            ? new Color(0.95f, 0.3f, 0.3f)
                            : new Color(0.4f, 0.85f, 0.4f);
                        listing.Label("ModCompatChecker.CurrentBalance".Translate() + bal.ToString("F2") + " " + ApiBalanceChecker.LastCurrency, -1);
                        GUI.color = Color.white;

                        if (bal <= settings.BalanceWarningThreshold)
                        {
                            GUI.color = new Color(0.9f, 0.5f, 0.1f);
                            listing.Label("ModCompatChecker.BalanceLow".Translate(), -1);
                            GUI.color = Color.white;
                        }
                    }
                    else if (!string.IsNullOrEmpty(ApiBalanceChecker.LastError))
                    {
                        GUI.color = new Color(0.7f, 0.7f, 0.4f);
                        listing.Label(ApiBalanceChecker.LastError, -1);
                        GUI.color = Color.white;
                    }
                }

                listing.Gap(4f);
                GUI.color = new Color(0.5f, 0.5f, 0.5f);
                listing.Label("ModCompatChecker.BalanceCheckHint".Translate(), -1);
                GUI.color = Color.white;
            }
        }

        public static void DrawApiMonitor(Listing_Standard listing, ref bool showMonitor, ref Vector2 monitorScroll)
        {
            var headerRect = listing.GetRect(30f);
            int running = ApiLogMonitor.RunningCount;
            string statusText = running > 0
                ? "ModCompatChecker.APIMonitor".Translate() + " [" + running + " " + "ModCompatChecker.Running".Translate() + "]"
                : "ModCompatChecker.APIMonitor".Translate();

            GUI.color = showMonitor
                ? new Color(0.22f, 0.28f, 0.42f)
                : new Color(0.14f, 0.18f, 0.28f);
            Widgets.DrawBoxSolid(headerRect, GUI.color);
            GUI.color = Color.white;

            string arrow = showMonitor ? "v" : ">";
            Widgets.Label(new Rect(headerRect.x + 10f, headerRect.y + 5f, headerRect.width - 20f, 20f), arrow + " " + statusText);

            if (Widgets.ButtonInvisible(headerRect))
                showMonitor = !showMonitor;

            if (showMonitor)
            {
                listing.Gap(2f);

                // Row 1: Force stop + Clear log (always visible)
                var stopRect = listing.GetRect(26f);
                if (running > 0)
                {
                    GUI.color = new Color(0.9f, 0.3f, 0.3f);
                }
                else
                {
                    GUI.color = new Color(0.5f, 0.3f, 0.3f);
                }
                if (Widgets.ButtonText(new Rect(stopRect.x, stopRect.y, 160f, 24f), "ModCompatChecker.ForceStopAll".Translate()))
                {
                    ApiLogMonitor.ForceStopAll();
                }
                GUI.color = Color.white;

                if (Widgets.ButtonText(new Rect(stopRect.x + 168f, stopRect.y, 80f, 24f), "ModCompatChecker.ClearLog".Translate()))
                    ApiLogMonitor.ClearLog();

                // Row 2: Block all subsequent API checkbox
                var blockRect = listing.GetRect(26f);
                bool wasBlocked = ApiLogMonitor.ApiBlocked;
                Widgets.CheckboxLabeled(blockRect, "ModCompatChecker.BlockAllAPI".Translate(), ref ApiLogMonitor.ApiBlocked);
                if (ApiLogMonitor.ApiBlocked && !wasBlocked)
                    ApiLogMonitor.SetApiBlocked(true);
                else if (!ApiLogMonitor.ApiBlocked && wasBlocked)
                    ApiLogMonitor.ApiBlocked = false;

                listing.Gap(4f);

                var entries = ApiLogMonitor.GetEntries();
                if (entries.Count == 0)
                {
                    listing.Label("ModCompatChecker.NoApiLogs".Translate(), -1);
                }
                else
                {
                    float entryHeight = 20f;
                    // Calculate actual content height accounting for detail sub-lines
                    float actualH = 0f;
                    foreach (var ent in entries)
                    {
                        actualH += entryHeight;
                        if (!string.IsNullOrEmpty(ent.Detail) && ent.Detail != "User cancelled" && ent.Detail != "Force stopped")
                            actualH += entryHeight;
                    }
                    float totalH = Math.Max(200f, actualH + 8f);
                    var scrollRect = listing.GetRect(Math.Min(300f, totalH));

                    Widgets.DrawBoxSolid(scrollRect, new Color(0.06f, 0.06f, 0.1f));
                    float contentH = actualH + 4f;
                    Widgets.BeginScrollView(
                        new Rect(scrollRect.x + 4f, scrollRect.y + 4f, scrollRect.width - 20f, scrollRect.height - 8f),
                        ref monitorScroll,
                        new Rect(0f, 0f, scrollRect.width - 24f, contentH));

                    float y = 0f;
                    for (int i = entries.Count - 1; i >= 0; i--)
                    {
                        var e = entries[i];
                        Color statusColor = e.Status switch
                        {
                            "Running" => new Color(0.4f, 0.7f, 1f),
                            "Completed" => new Color(0.4f, 0.85f, 0.4f),
                            "Failed" => new Color(0.95f, 0.4f, 0.4f),
                            "Cancelled" => new Color(0.7f, 0.7f, 0.3f),
                            _ => Color.grey
                        };

                        GUI.color = statusColor;
                        string line = string.Format("{0:HH:mm:ss} [{1}] {2}",
                            e.Timestamp, e.Status, e.OperationType);
                        Widgets.Label(new Rect(0f, y, scrollRect.width - 28f, entryHeight), line);
                        y += entryHeight;

                        if (!string.IsNullOrEmpty(e.Detail) && e.Detail != "User cancelled" && e.Detail != "Force stopped")
                        {
                            GUI.color = new Color(0.5f, 0.5f, 0.5f);
                            Widgets.Label(new Rect(14f, y, scrollRect.width - 42f, entryHeight),
                                "  " + e.Detail);
                            y += entryHeight;
                        }
                    }

                    GUI.color = Color.white;
                    Widgets.EndScrollView();
                }
            }
        }
    }
}

