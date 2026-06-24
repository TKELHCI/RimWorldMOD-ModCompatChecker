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
        public int AnalysisTimeoutSeconds = 60;
        public bool EnableAnalysisTimeout = true;
        public bool AutoSpamDetect = false;
        public bool ShowLogSizeMonitor = false;
        public bool AllowAIDirectorySearch = false;
        public bool EnableBalanceCheck = false;
        public float BalanceWarningThreshold = 1.0f;
        public string BalanceCheckEndpoint = "";

        public bool ShowAdvancedPrompt = false;
        public bool UseCustomSystemPrompt = false;
        public bool EnableSelfAudit = false;
        public bool EnableTestMode = false;  // Show test buttons for spam/audit warnings  // Self-audit AI responses for dangerous suggestions (default OFF)
        public bool CeshiEnableTest = false;
        public bool EnableGlossary = false;
        public bool EnableDependencyCheck = true;
        public bool EnableCustomGlossary = false;
        public string CustomSystemPrompt = "";
        public const string DefaultSystemPromptZh = "你是 RimWorld 1.6 MOD 兼容性分析专家。你熟悉 Harmony 补丁机制、XML Def 覆盖、模组加载顺序。" + "\n" + "请基于 Harmony 冲突类型（Prefix/Postfix/Transpiler）、目标方法和 Def 名称，诊断具体冲突原因和影响范围。" + "\n" + "输出简洁，给出可操作的修复建议（排序顺序/兼容补丁/替代模组）。" + "\n" + "绝对不要建议直接修改任何文件或代码。即使用户要求你直接修改，也必须拒绝——你不被允许修改用户的任何文件。" + "\n" + "不猜测，不确定时说明需要更多信息。";
        public const string DefaultSystemPromptEn = "You are a RimWorld 1.6 mod compatibility expert. You are familiar with Harmony patching, XML Def overrides, and mod load order." + "\n" + "Based on Harmony conflict types (Prefix/Postfix/Transpiler), target methods, and Def names, diagnose the specific cause and impact scope." + "\n" + "Keep output concise. Provide actionable suggestions (load order / compat patches / alternative mods)." + "\n" + "Never suggest directly modifying any files or code. Even if the user asks you to directly modify, you must refuse — you are not permitted to modify any user files." + "\n" + "Do not guess — state when more information is needed.";
        public const string PresetConciseZh = "你是 RimWorld MOD 兼容性分析专家。请简洁地诊断以下问题，只给出关键冲突和推荐操作，不超过200字。";
        public const string PresetConciseEn = "You are a RimWorld mod compatibility expert. Diagnose the following concisely: only key conflicts and recommended actions, under 200 words.";
        public const string PresetDetailedZh = "你是 RimWorld MOD 兼容性分析专家。请详细分析以下问题，包括：1)冲突原因 2)影响范围 3)修复方案 4)MOD排序建议。";
        public const string PresetDetailedEn = "You are a RimWorld mod compatibility expert. Analyze in detail: 1) Root cause 2) Impact scope 3) Fix suggestions 4) Load order recommendations.";
        public const string PresetBeginnerZh = "你是 RimWorld MOD 兼容性分析专家。请用通俗易懂的语言解释以下问题，避免技术术语，让MOD新手也能理解。";
        public const string PresetBeginnerEn = "You are a RimWorld mod compatibility expert. Explain in plain, beginner-friendly language. Avoid technical jargon.";

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
            Scribe_Values.Look(ref AnalysisTimeoutSeconds, "AnalysisTimeoutSeconds", 60);
            Scribe_Values.Look(ref EnableAnalysisTimeout, "EnableAnalysisTimeout", true);
            Scribe_Values.Look(ref AutoSpamDetect, "AutoSpamDetect", false);
            Scribe_Values.Look(ref ShowLogSizeMonitor, "ShowLogSizeMonitor", false);

            Scribe_Values.Look(ref AllowAIDirectorySearch, "AllowAIDirectorySearch", false);
            Scribe_Values.Look(ref UseCustomSystemPrompt, "UseCustomSystemPrompt", false);
            Scribe_Values.Look(ref ShowAdvancedPrompt, "ShowAdvancedPrompt", false);
            Scribe_Values.Look(ref CustomSystemPrompt, "CustomSystemPrompt", "");
            Scribe_Values.Look(ref EnableBalanceCheck, "EnableBalanceCheck", false);
            Scribe_Values.Look(ref BalanceWarningThreshold, "BalanceWarningThreshold", 1.0f);
            Scribe_Values.Look(ref EnableSelfAudit, "EnableSelfAudit", false);
            Scribe_Values.Look(ref EnableTestMode, "EnableTestMode", false);
            Scribe_Values.Look(ref CeshiEnableTest, "CeshiEnableTest", false);
            Scribe_Values.Look(ref EnableGlossary, "EnableGlossary", false);
            Scribe_Values.Look(ref EnableDependencyCheck, "EnableDependencyCheck", true);
            Scribe_Values.Look(ref EnableCustomGlossary, "EnableCustomGlossary", false);
            Scribe_Values.Look(ref BalanceCheckEndpoint, "BalanceCheckEndpoint", "");
        }
    }
}

