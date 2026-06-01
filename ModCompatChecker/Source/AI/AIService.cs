using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using ModCompatChecker.Core;
using Verse;

namespace ModCompatChecker.AI
{
    public static class AIService
    {
        public static string AnalyzeHarmonyConflict(
            HarmonyConflict conflict, string apiEndpoint, string apiKey,
            string modelId, ModelConfig.ApiProvider provider)
        {
            var prompt = PromptBuilder.BuildHarmonyConflictPrompt(conflict, PromptBuilder.GetPromptLanguage());
            bool cancel = false;
            return CallAPIWithTimeout(apiEndpoint, apiKey, modelId, prompt, provider, 30, ref cancel);
        }

        public static string AnalyzeDefConflict(
            DefConflict conflict, string apiEndpoint, string apiKey,
            string modelId, ModelConfig.ApiProvider provider)
        {
            var prompt = PromptBuilder.BuildDefConflictPrompt(conflict, PromptBuilder.GetPromptLanguage());
            bool cancel = false;
            return CallAPIWithTimeout(apiEndpoint, apiKey, modelId, prompt, provider, 30, ref cancel);
        }

        public static string AnalyzeError(
            string errorStack, ConflictReport report,
            string apiEndpoint, string apiKey, string modelId,
            ModelConfig.ApiProvider provider)
        {
            var prompt = PromptBuilder.BuildErrorAnalysisPrompt(errorStack, report, PromptBuilder.GetPromptLanguage());
            bool cancel = false;
            return CallAPIWithTimeout(apiEndpoint, apiKey, modelId, prompt, provider, 30, ref cancel);
        }

        public static string AnalyzeDependencyIssue(
            DependencyIssue issue, string apiEndpoint, string apiKey,
            string modelId, ModelConfig.ApiProvider provider)
        {
            var prompt = PromptBuilder.BuildDependencyIssuePrompt(issue, PromptBuilder.GetPromptLanguage());
            bool cancel = false;
            return CallAPIWithTimeout(apiEndpoint, apiKey, modelId, prompt, provider, 30, ref cancel);
        }

        public static string TestConnection(
            string apiEndpoint, string apiKey, string modelId,
            ModelConfig.ApiProvider provider)
        {
            bool cancel = false;
            return CallAPIWithTimeout(apiEndpoint, apiKey, modelId,
                "Say 'Connection OK' in one word.", provider, 10, ref cancel);
        }

        public static string CallAPIWithTimeout(
            string endpoint, string apiKey, string modelId,
            string userMessage, ModelConfig.ApiProvider provider,
            int timeoutSeconds, ref bool cancelFlag)
        {
            bool isAnthropic = provider == ModelConfig.ApiProvider.Anthropic;
            string requestBody = isAnthropic
                ? BuildAnthropicBody(modelId, userMessage)
                : BuildOpenAIBody(modelId, userMessage);

            var request = (HttpWebRequest)WebRequest.Create(endpoint);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Timeout = timeoutSeconds * 1000;

            if (isAnthropic)
            {
                request.Headers["x-api-key"] = apiKey;
                request.Headers["anthropic-version"] = "2023-06-01";
            }
            else
            {
                request.Headers["Authorization"] = "Bearer " + apiKey;
            }

            var bodyBytes = Encoding.UTF8.GetBytes(requestBody);
            request.ContentLength = bodyBytes.Length;

            var asyncResult = request.BeginGetRequestStream(null, null);
            int waited = 0;
            while (!asyncResult.IsCompleted)
            {
                if (cancelFlag) { TryAbort(request); return "ModCompatChecker.AnalysisCancelled".Translate(); }
                Thread.Sleep(100);
                waited += 100;
                if (waited > timeoutSeconds * 1000)
                { TryAbort(request); return "ModCompatChecker.RequestTimeout".Translate() + timeoutSeconds + "ModCompatChecker.AutoCancelled".Translate(); }
            }

            using (var stream = request.EndGetRequestStream(asyncResult))
                stream.Write(bodyBytes, 0, bodyBytes.Length);

            var respResult = request.BeginGetResponse(null, null);
            waited = 0;
            while (!respResult.IsCompleted)
            {
                if (cancelFlag) { TryAbort(request); return "ModCompatChecker.AnalysisCancelled".Translate(); }
                Thread.Sleep(100);
                waited += 100;
                if (waited > timeoutSeconds * 1000)
                { TryAbort(request); return "ModCompatChecker.ResponseTimeout".Translate() + timeoutSeconds + "ModCompatChecker.AutoCancelled".Translate(); }
            }

            try
            {
                using (var response = (HttpWebResponse)request.EndGetResponse(respResult))
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    return ParseResponse(reader.ReadToEnd(), isAnthropic);
                }
            }
            catch (WebException wex)
            {
                if (cancelFlag) return "ModCompatChecker.AnalysisCancelled".Translate();
                var httpResp = wex.Response as HttpWebResponse;
                if (httpResp != null)
                {
                    int code = (int)httpResp.StatusCode;
                    if (code == 401 || code == 403)
                        return "ModCompatChecker.NetworkError".Translate() + "HTTP " + code + " - " + "ModCompatChecker.AuthError".Translate();
                    if (code == 429)
                        return "ModCompatChecker.NetworkError".Translate() + "HTTP 429 - " + "ModCompatChecker.RateLimit".Translate();
                    if (code >= 500)
                        return "ModCompatChecker.NetworkError".Translate() + "HTTP " + code + " - " + "ModCompatChecker.ServerError".Translate();
                }
                return "ModCompatChecker.NetworkError".Translate() + wex.Message;
            }
        }

        private static void TryAbort(HttpWebRequest request)
        {
            try { request.Abort(); } catch { }
        }

        private static string BuildOpenAIBody(string model, string userMessage)
        {
            var escaped = EscapeJson(userMessage);
            return "{\"model\":\"" + model + "\",\"messages\":[{\"role\":\"user\",\"content\":\"" + escaped + "\"}],\"max_tokens\":1500,\"temperature\":0.3}";
        }

        private static string BuildAnthropicBody(string model, string userMessage)
        {
            var escaped = EscapeJson(userMessage);
            return "{\"model\":\"" + model + "\",\"max_tokens\":1500,\"messages\":[{\"role\":\"user\",\"content\":\"" + escaped + "\"}]}";
        }

        private static string ParseResponse(string json, bool isAnthropic)
        {
            try
            {
                if (isAnthropic)
                    return ExtractJsonValue(json, "text");
                else
                {
                    var content = ExtractJsonValue(json, "content");
                    if (!string.IsNullOrEmpty(content) && content != json)
                        return content;
                    var msgStart = json.IndexOf("\"message\"");
                    if (msgStart > 0)
                    {
                        content = ExtractJsonValue(json.Substring(msgStart), "content");
                        if (!string.IsNullOrEmpty(content) && content != json.Substring(msgStart))
                            return content;
                    }
                    return Truncate(json, 500);
                }
            }
            catch
            {
                return Truncate(json, 500);
            }
        }

        private static string ExtractJsonValue(string json, string key)
        {
            var searchKey = "\"" + key + "\":\"";
            var idx = json.IndexOf(searchKey, StringComparison.Ordinal);
            if (idx < 0)
            {
                searchKey = "\"" + key + "\": \"";
                idx = json.IndexOf(searchKey, StringComparison.Ordinal);
            }
            if (idx < 0) return json;

            var start = idx + searchKey.Length;
            var sb = new StringBuilder();
            for (int i = start; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '\\' && i + 1 < json.Length)
                {
                    char next = json[i + 1];
                    switch (next)
                    {
                        case 'n': sb.Append('\n'); i++; break;
                        case 'r': sb.Append('\r'); i++; break;
                        case 't': sb.Append('\t'); i++; break;
                        case '"': sb.Append('"'); i++; break;
                        case '\\': sb.Append('\\'); i++; break;
                        case '/': sb.Append('/'); i++; break;
                        default: sb.Append(next); i++; break;
                    }
                }
                else if (c == '"')
                {
                    break;
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private static string EscapeJson(string text)
        {
            return text.Replace("\\", "\\\\").Replace("\"", "\\\"")
                .Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
        }


        private static string Truncate(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLen) return text;
            return text.Substring(0, maxLen) + "...";
        }
    }
}
