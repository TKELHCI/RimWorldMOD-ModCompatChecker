using System.Collections.Generic;
using System.IO;
using System.Xml;
using Verse;

namespace ModCompatChecker.AI
{
    public static class ModelConfig
    {
        public enum ApiProvider
        {
            OpenAI,
            Anthropic,
            DeepSeek,
            Google,
            Kimi,
            Qwen,
            GLM,
            OpenRouter,
            Custom
        }

        public class ModelInfo
        {
            public string Id;
            public string DisplayKey;
            public ApiProvider Provider;
            public string DefaultEndpoint;
            public bool IsDefault;
        }

        private static List<ModelInfo> _models;
        public static List<ModelInfo> Models
        {
            get
            {
                if (_models == null) _models = LoadModels();
                return _models;
            }
        }

        private static List<ModelInfo> LoadModels()
        {
            try
            {
                var jsonPath = Path.Combine(ModCompatChecker.ModCompatMod.Instance.Content.RootDir, "Assemblies", "Models.json");
                if (File.Exists(jsonPath))
                {
                    var json = File.ReadAllText(jsonPath);
                    return ParseModelJson(json);
                }
            }
            catch { /* fallback to built-in */ }
            return GetBuiltInModels();
        }

        private static List<ModelInfo> ParseModelJson(string json)
        {
            var list = new List<ModelInfo>();
            var doc = new XmlDocument();
            int idx = 0;
            while (idx < json.Length)
            {
                int objStart = json.IndexOf('{', idx);
                if (objStart < 0) break;
                int objEnd = FindMatchingBrace(json, objStart);
                if (objEnd < 0) break;
                var obj = json.Substring(objStart, objEnd - objStart + 1);
                var info = new ModelInfo();
                info.Id = ExtractJsonString(obj, "id");
                info.DisplayKey = ExtractJsonString(obj, "displayKey");
                var prov = ExtractJsonString(obj, "provider");
                info.Provider = prov == "Anthropic" ? ApiProvider.Anthropic
                    : prov == "OpenAI" ? ApiProvider.OpenAI
                    : prov == "DeepSeek" ? ApiProvider.DeepSeek
                    : prov == "Google" ? ApiProvider.Google
                    : prov == "Kimi" ? ApiProvider.Kimi
                    : prov == "Qwen" ? ApiProvider.Qwen
                    : prov == "GLM" ? ApiProvider.GLM
                    : prov == "OpenRouter" ? ApiProvider.OpenRouter : ApiProvider.Custom;
                info.DefaultEndpoint = ExtractJsonString(obj, "defaultEndpoint");
                info.IsDefault = obj.Contains("\"isDefault\": true") || obj.Contains("\"isDefault\":true");
                if (!string.IsNullOrEmpty(info.Id)) list.Add(info);
                idx = objEnd + 1;
            }
            return list.Count > 0 ? list : GetBuiltInModels();
        }

        private static int FindMatchingBrace(string s, int start)
        {
            int depth = 0;
            bool inStr = false;
            for (int i = start; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '"' && (i == start || s[i-1] != '\\')) inStr = !inStr;
                if (inStr) continue;
                if (c == '{') depth++;
                else if (c == '}') { depth--; if (depth == 0) return i; }
            }
            return -1;
        }

        private static string ExtractJsonString(string json, string key)
        {
            var search = "\"" + key + "\":\"";
            int idx = json.IndexOf(search);
            if (idx < 0) { search = "\"" + key + "\": \""; idx = json.IndexOf(search); }
            if (idx < 0) return "";
            idx += search.Length;
            int end = json.IndexOf('"', idx);
            if (end < 0) return "";
            return json.Substring(idx, end - idx);
        }

        private static List<ModelInfo> GetBuiltInModels()
        {
            return new List<ModelInfo>
            {
                // DeepSeek
                new ModelInfo { Id = "deepseek-v4-flash", DisplayKey = "ModCompatChecker.ModelDeepSeekChat", Provider = ApiProvider.DeepSeek, DefaultEndpoint = "https://api.deepseek.com/v1/chat/completions", IsDefault = true },
                new ModelInfo { Id = "deepseek-v4-pro", DisplayKey = "ModCompatChecker.ModelDeepSeekReasoner", Provider = ApiProvider.DeepSeek, DefaultEndpoint = "https://api.deepseek.com/v1/chat/completions", IsDefault = false },
                // OpenAI
                new ModelInfo { Id = "gpt-4.1-nano", DisplayKey = "ModCompatChecker.ModelGPT41Nano", Provider = ApiProvider.OpenAI, DefaultEndpoint = "https://api.openai.com/v1/chat/completions", IsDefault = false },
                new ModelInfo { Id = "gpt-4.1-mini", DisplayKey = "ModCompatChecker.ModelGPT41Mini", Provider = ApiProvider.OpenAI, DefaultEndpoint = "https://api.openai.com/v1/chat/completions", IsDefault = false },
                new ModelInfo { Id = "gpt-4o-mini", DisplayKey = "ModCompatChecker.ModelGPT4oMini", Provider = ApiProvider.OpenAI, DefaultEndpoint = "https://api.openai.com/v1/chat/completions", IsDefault = false },
                new ModelInfo { Id = "gpt-4o", DisplayKey = "ModCompatChecker.ModelGPT4o", Provider = ApiProvider.OpenAI, DefaultEndpoint = "https://api.openai.com/v1/chat/completions", IsDefault = false },
                // Anthropic
                new ModelInfo { Id = "claude-3-5-haiku-20241022", DisplayKey = "ModCompatChecker.ModelClaude35Haiku", Provider = ApiProvider.Anthropic, DefaultEndpoint = "https://api.anthropic.com/v1/messages", IsDefault = false },
                new ModelInfo { Id = "claude-3-5-sonnet-20241022", DisplayKey = "ModCompatChecker.ModelClaude35Sonnet", Provider = ApiProvider.Anthropic, DefaultEndpoint = "https://api.anthropic.com/v1/messages", IsDefault = false },
                new ModelInfo { Id = "claude-sonnet-4", DisplayKey = "ModCompatChecker.ModelClaudeSonnet4", Provider = ApiProvider.Anthropic, DefaultEndpoint = "https://api.anthropic.com/v1/messages", IsDefault = false },
                // Google Gemini
                new ModelInfo { Id = "gemini-2.5-flash", DisplayKey = "ModCompatChecker.ModelGemini25Flash", Provider = ApiProvider.Google, DefaultEndpoint = "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions", IsDefault = false },
                new ModelInfo { Id = "gemini-2.5-pro", DisplayKey = "ModCompatChecker.ModelGemini25Pro", Provider = ApiProvider.Google, DefaultEndpoint = "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions", IsDefault = false },
                // Kimi / Moonshot
                new ModelInfo { Id = "moonshot-v1-8k", DisplayKey = "ModCompatChecker.ModelKimi8K", Provider = ApiProvider.Kimi, DefaultEndpoint = "https://api.moonshot.cn/v1/chat/completions", IsDefault = false },
                new ModelInfo { Id = "moonshot-v1-32k", DisplayKey = "ModCompatChecker.ModelKimi32K", Provider = ApiProvider.Kimi, DefaultEndpoint = "https://api.moonshot.cn/v1/chat/completions", IsDefault = false },
                // Qwen / DashScope
                new ModelInfo { Id = "qwen-plus", DisplayKey = "ModCompatChecker.ModelQwenPlus", Provider = ApiProvider.Qwen, DefaultEndpoint = "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions", IsDefault = false },
                new ModelInfo { Id = "qwen3.7-max", DisplayKey = "ModCompatChecker.ModelQwen37Max", Provider = ApiProvider.Qwen, DefaultEndpoint = "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions", IsDefault = false },
                // GLM / Zhipu
                new ModelInfo { Id = "glm-4-flash", DisplayKey = "ModCompatChecker.ModelGLM4Flash", Provider = ApiProvider.GLM, DefaultEndpoint = "https://open.bigmodel.cn/api/paas/v4/chat/completions", IsDefault = false },
                new ModelInfo { Id = "glm-4-plus", DisplayKey = "ModCompatChecker.ModelGLM4Plus", Provider = ApiProvider.GLM, DefaultEndpoint = "https://open.bigmodel.cn/api/paas/v4/chat/completions", IsDefault = false },
                // OpenRouter
                new ModelInfo { Id = "openai/gpt-4o", DisplayKey = "ModCompatChecker.ModelORGPT4o", Provider = ApiProvider.OpenRouter, DefaultEndpoint = "https://openrouter.ai/api/v1/chat/completions", IsDefault = false },
                new ModelInfo { Id = "anthropic/claude-sonnet-4", DisplayKey = "ModCompatChecker.ModelORClaudeSonnet4", Provider = ApiProvider.OpenRouter, DefaultEndpoint = "https://openrouter.ai/api/v1/chat/completions", IsDefault = false },
                new ModelInfo { Id = "google/gemini-2.5-flash", DisplayKey = "ModCompatChecker.ModelORGemini25Flash", Provider = ApiProvider.OpenRouter, DefaultEndpoint = "https://openrouter.ai/api/v1/chat/completions", IsDefault = false },
            };
        }

        public static ModelInfo GetDefaultModel() => Models.Find(m => m.IsDefault) ?? Models[0];
        
        public static string GetProviderGroupName(ApiProvider provider)
        {
            switch (provider)
            {
                case ApiProvider.DeepSeek: return "DeepSeek";
                case ApiProvider.OpenAI: return "OpenAI";
                case ApiProvider.Anthropic: return "Anthropic";
                case ApiProvider.Google: return "Google Gemini";
                case ApiProvider.Kimi: return "Kimi / Moonshot";
                case ApiProvider.Qwen: return "Qwen \u901a\u4e49\u5343\u95ee";
                case ApiProvider.GLM: return "GLM / \u667a\u8c31";
                case ApiProvider.OpenRouter: return "OpenRouter";
                default: return "Custom";
            }
        }

        public static List<ApiProvider> GetProviderOrder()
        {
            return new List<ApiProvider>
            {
                ApiProvider.DeepSeek,
                ApiProvider.OpenAI,
                ApiProvider.Anthropic,
                ApiProvider.Google,
                ApiProvider.Kimi,
                ApiProvider.Qwen,
                ApiProvider.GLM,
                ApiProvider.OpenRouter
            };
        }
        public static ModelInfo FindModel(string id) => Models.Find(m => m.Id == id);
    }
}