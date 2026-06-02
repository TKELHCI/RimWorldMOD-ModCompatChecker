using System;
using System.Collections.Generic;
using System.Threading;
using ModCompatChecker.AI;
using ModCompatChecker.UI;
using UnityEngine;
using Verse;

namespace ModCompatChecker
{
    public class ModCompatSettings : ModSettings
    {
        public string APIEndpoint = "https://api.deepseek.com/v1/chat/completions";
        public string APIKey = "";
        public string CustomModelId = "";
        public int SelectedModelIndex = 0;
        public int AnalysisTimeoutSeconds = 20;
        public bool AutoSpamDetect = false;

        public bool UseCustomSystemPrompt = false;
        public string CustomSystemPrompt = "";
        public const string DefaultSystemPromptZh = "你是 RimWorld MOD 兼容性分析专家。请基于你的知识分析以下 MOD 兼容性问题，给出专业、简洁的诊断和建议。";
        public const string DefaultSystemPromptEn = "You are a RimWorld mod compatibility expert. Based on your knowledge, analyze the following mod compatibility issues and provide professional, concise diagnosis and suggestions.";

        private readonly SharedSettingsUI.UIState _uiState = new SharedSettingsUI.UIState();

        public void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            SharedSettingsUI.DrawModelSelector(listing, this, _uiState);
            listing.Gap(12f);
            SharedSettingsUI.DrawAPISettings(listing, this, _uiState);
            listing.Gap(10f);
            SharedSettingsUI.DrawTestConnection(listing, this, _uiState);
            listing.Gap(16f);

            GUI.color = new Color(0.3f, 0.55f, 0.3f);
            Widgets.DrawBoxSolid(listing.GetRect(2f), GUI.color);
            GUI.color = Color.white;
            listing.Gap(4f);
            listing.Label("ModCompatChecker.QuickEntry".Translate(), -1);

            if (listing.ButtonText("ModCompatChecker.OpenCompatChecker".Translate()))
                Find.WindowStack.Add(new ModCompatLandingWindow());
            listing.Gap(4f);
            if (listing.ButtonText("ModCompatChecker.OpenUnifiedWindow".Translate()))
                Find.WindowStack.Add(new UnifiedWindow());

            listing.End();
        }

        public void SetDisposed() { _uiState.Disposed = true; }

        public (string endpoint, ModelConfig.ApiProvider provider) GetEndpointAndProvider(ModelConfig.ModelInfo modelInfo)
        {
            if (IsCustomModel()) return (APIEndpoint, ModelConfig.ApiProvider.Custom);
            return (modelInfo.DefaultEndpoint, modelInfo.Provider);
        }

        public ModelConfig.ModelInfo GetSelectedModelInfo()
        {
            if (SelectedModelIndex >= 0 && SelectedModelIndex < ModelConfig.Models.Count)
                return ModelConfig.Models[SelectedModelIndex];
            return ModelConfig.GetDefaultModel();
        }

        public bool IsCustomModel() => SelectedModelIndex >= ModelConfig.Models.Count;
        public bool IsAIConfigured() => !string.IsNullOrEmpty(APIKey);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref APIEndpoint, "APIEndpoint", "https://api.deepseek.com/v1/chat/completions");
            Scribe_Values.Look(ref APIKey, "APIKey", "");
            Scribe_Values.Look(ref CustomModelId, "CustomModelId", "");
            Scribe_Values.Look(ref SelectedModelIndex, "SelectedModelIndex", 0);
            Scribe_Values.Look(ref AnalysisTimeoutSeconds, "AnalysisTimeoutSeconds", 20);
            Scribe_Values.Look(ref AutoSpamDetect, "AutoSpamDetect", false);

            Scribe_Values.Look(ref UseCustomSystemPrompt, "UseCustomSystemPrompt", false);
            Scribe_Values.Look(ref CustomSystemPrompt, "CustomSystemPrompt", "");
        }
    }
}