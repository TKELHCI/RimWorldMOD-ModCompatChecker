using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Verse;

namespace ModCompatChecker.Core
{
    public static class ApiBalanceChecker
    {
        private static float _lastBalance = -1f;
        private static string _lastCurrency = "";
        private static string _lastError = "";
        private static DateTime _lastCheckTime = DateTime.MinValue;
        private static bool _isChecking;
        private static readonly object _lock = new object();

        public static float LastBalance { get { lock (_lock) return _lastBalance; } }
        public static string LastCurrency { get { lock (_lock) return _lastCurrency; } }
        public static string LastError { get { lock (_lock) return _lastError; } }
        public static DateTime LastCheckTime { get { lock (_lock) return _lastCheckTime; } }
        public static bool IsChecking { get { lock (_lock) return _isChecking; } }
        public static bool WarningSent { get; set; }

        public static void CheckBalance(string apiEndpoint, string apiKey)
        {
            lock (_lock)
            {
                if (_isChecking) return;
                _isChecking = true;
            }

            var balLog = ApiLogMonitor.LogStart("余额检测");

            new Thread(() =>
            {
                try
                {
                    string balanceUrl = GetBalanceUrl(apiEndpoint, apiKey);
                    var request = (HttpWebRequest)WebRequest.Create(balanceUrl);
                    request.Method = "GET";
                    request.Timeout = 10000;
                    request.Headers["Authorization"] = "Bearer " + apiKey;

                    using (var response = (HttpWebResponse)request.GetResponse())
                    using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                    {
                        string json = reader.ReadToEnd();
                        ParseBalanceResponse(json);
                    }

                    // Log result
                    float bal; string cur;
                    lock (_lock) { bal = _lastBalance; cur = _lastCurrency; }
                    if (bal >= 0)
                        ApiLogMonitor.LogComplete(balLog, bal.ToString("F2") + " " + cur);
                    else
                        ApiLogMonitor.LogFailed(balLog, _lastError ?? "Unknown");
                }
                catch (WebException wex)
                {
                    var httpResp = wex.Response as HttpWebResponse;
                    string errMsg;
                    if (httpResp != null)
                    {
                        errMsg = "HTTP " + (int)httpResp.StatusCode;
                        lock (_lock) { _lastError = errMsg; }
                    }
                    else
                    {
                        errMsg = wex.Message;
                        lock (_lock) { _lastError = errMsg; }
                    }
                    ApiLogMonitor.LogFailed(balLog, errMsg);
                }
                catch (Exception ex)
                {
                    lock (_lock) { _lastError = ex.Message; }
                    ApiLogMonitor.LogFailed(balLog, ex.Message);
                }
                finally
                {
                    lock (_lock) { _isChecking = false; _lastCheckTime = DateTime.Now; }
                }
            }) { IsBackground = true }.Start();
        }

        private static string GetBalanceUrl(string chatEndpoint, string apiKey)
        {
            if (chatEndpoint.Contains("deepseek.com"))
                return "https://api.deepseek.com/user/balance";
            if (chatEndpoint.Contains("openai.com"))
                return "https://api.openai.com/v1/usage?date=" + DateTime.UtcNow.ToString("yyyy-MM-dd");
            if (chatEndpoint.Contains("anthropic.com"))
                return "https://api.anthropic.com/v1/models";
            Uri uri;
            if (Uri.TryCreate(chatEndpoint, UriKind.Absolute, out uri))
                return uri.GetLeftPart(UriPartial.Authority) + "/v1/usage";
            return chatEndpoint;
        }

        private static void ParseBalanceResponse(string json)
        {
            // DeepSeek format
            if (json.Contains("balance_infos"))
            {
                var currencyMatch = Regex.Match(json, @"""currency""\s*:\s*""([^""]+)""");
                var balanceMatch = Regex.Match(json, @"""total_balance""\s*:\s*""([^""]+)""");
                if (balanceMatch.Success)
                {
                    float bal;
                    if (float.TryParse(balanceMatch.Groups[1].Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out bal))
                    {
                        lock (_lock)
                        {
                            _lastBalance = bal;
                            _lastCurrency = currencyMatch.Success ? currencyMatch.Groups[1].Value : "CNY";
                            _lastError = "";
                            WarningSent = false;
                        }
                        return;
                    }
                }
            }

            // OpenAI format
            if (json.Contains("total_usage"))
            {
                var match = Regex.Match(json, @"""total_usage""\s*:\s*([0-9.]+)");
                if (match.Success)
                {
                    float usage;
                    if (float.TryParse(match.Groups[1].Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out usage))
                    {
                        lock (_lock)
                        {
                            _lastBalance = usage / 100f;
                            _lastCurrency = "USD";
                            _lastError = "";
                            WarningSent = false;
                        }
                    }
                }
                return;
            }

            // Anthropic / unknown
            if (json.Contains("data") && json.Contains("model"))
            {
                lock (_lock) { _lastError = "BalanceCheckNotSupported".Translate(); }
                return;
            }

            lock (_lock) { _lastError = "BalanceCheckParseError".Translate(); }
        }
    }
}