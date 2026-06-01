using System;
using System.Collections.Generic;
using System.Threading;
using ModCompatChecker.AI;
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
                var modelNames = new List<string>();
                foreach (var m in ModelConfig.Models)
                    modelNames.Add(m.DisplayKey.Translate().ToString());
                modelNames.Add("ModCompatChecker.CustomModel".Translate());

                if (settings.SelectedModelIndex >= modelNames.Count)
                    settings.SelectedModelIndex = 0;

                for (int i = 0; i < modelNames.Count; i++)
                {
                    var rowRect = listing.GetRect(24f);
                    string prefix = settings.SelectedModelIndex == i ? "O" : "o";
                    GUI.color = settings.SelectedModelIndex == i
                        ? new Color(0.3f, 0.7f, 0.3f)
                        : new Color(0.5f, 0.5f, 0.5f);

                    if (Widgets.ButtonText(new Rect(rowRect.x + 16f, rowRect.y, rowRect.width - 20f, 22f),
                        prefix + "  " + modelNames[i]))
                    {
                        settings.SelectedModelIndex = i;
                        if (i < ModelConfig.Models.Count)
                            settings.APIEndpoint = ModelConfig.Models[i].DefaultEndpoint;
                    }
                    GUI.color = Color.white;
                }
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
            settings.APIKey = listing.TextEntry(settings.APIKey);
            listing.Gap(3f);

            listing.Label("ModCompatChecker.AnalysisTimeout".Translate() + settings.AnalysisTimeoutSeconds + "ModCompatChecker.SecondsUnit".Translate(), -1);
            var tsStr = listing.TextEntry(settings.AnalysisTimeoutSeconds.ToString());
            if (int.TryParse(tsStr, out var ts) && ts >= 5 && ts <= 120)
                settings.AnalysisTimeoutSeconds = ts;
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
            state.TestResult = "";
            state.ShowTestResult = true;

            var modelInfo = settings.GetSelectedModelInfo();
            var ep = settings.GetEndpointAndProvider(modelInfo);
            var apiKey = settings.APIKey;
            var endpoint = ep.endpoint;
            var provider = ep.provider;
            var modelId = settings.IsCustomModel() ? settings.CustomModelId : modelInfo.Id;

            var thread = new Thread(() =>
            {
                try
                {
                    var result = AIService.TestConnection(endpoint, apiKey, modelId, provider);
                    if (!state.Disposed) lock (state.Lock)
                    {
                        state.TestResult = (result.Contains("OK") || result.Contains("Connection OK") || result.Length < 50)
                            ? "ModCompatChecker.ConnectionSuccess".Translate() : "ModCompatChecker.ConnectionFailed".Translate() + result;
                    }
                }
                catch (Exception ex)
                {
                    if (!state.Disposed) lock (state.Lock) { state.TestResult = "ModCompatChecker.ConnectionFailed".Translate() + ex.Message; }
                }
                if (!state.Disposed) lock (state.Lock) { state.IsTesting = false; }
                if (!state.Disposed) state.ShowTestResult = true;
            }) { IsBackground = true };
            thread.Start();
        }
    }
}


