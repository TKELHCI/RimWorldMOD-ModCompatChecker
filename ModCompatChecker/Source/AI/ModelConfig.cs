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
            // Simple manual JSON array parse (avoids needing Newtonsoft.Json)
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
                    : prov == "DeepSeek" ? ApiProvider.DeepSeek : ApiProvider.Custom;
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
                new ModelInfo { Id = "deepseek-v4-flash", DisplayKey = "ModCompatChecker.ModelDeepSeekFlash", Provider = ApiProvider.DeepSeek, DefaultEndpoint = "https://api.deepseek.com/v1/chat/completions", IsDefault = true },
                new ModelInfo { Id = "deepseek-v4-pro", DisplayKey = "ModCompatChecker.ModelDeepSeekPro", Provider = ApiProvider.DeepSeek, DefaultEndpoint = "https://api.deepseek.com/v1/chat/completions", IsDefault = false },
                new ModelInfo { Id = "gpt-4o-mini", DisplayKey = "ModCompatChecker.ModelGPT4oMini", Provider = ApiProvider.OpenAI, DefaultEndpoint = "https://api.openai.com/v1/chat/completions", IsDefault = false },
                new ModelInfo { Id = "gpt-4o", DisplayKey = "ModCompatChecker.ModelGPT4o", Provider = ApiProvider.OpenAI, DefaultEndpoint = "https://api.openai.com/v1/chat/completions", IsDefault = false },
                new ModelInfo { Id = "claude-3-haiku-20240307", DisplayKey = "ModCompatChecker.ModelClaudeHaiku", Provider = ApiProvider.Anthropic, DefaultEndpoint = "https://api.anthropic.com/v1/messages", IsDefault = false },
                new ModelInfo { Id = "claude-3-5-sonnet-20240620", DisplayKey = "ModCompatChecker.ModelClaudeSonnet", Provider = ApiProvider.Anthropic, DefaultEndpoint = "https://api.anthropic.com/v1/messages", IsDefault = false },
            };
        }

        public static ModelInfo GetDefaultModel() => Models.Find(m => m.IsDefault) ?? Models[0];
        public static ModelInfo FindModel(string id) => Models.Find(m => m.Id == id);
    }
}