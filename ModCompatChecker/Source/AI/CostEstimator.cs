using System;
using System.Collections.Generic;

namespace ModCompatChecker.AI
{
    /// <summary>
    /// Token 计数和 API 费用估算
    /// 参考 DeepSeek V4 定价: Flash ~¥0.001/1K tokens, Pro ~¥0.004/1K tokens
    /// </summary>
    public static class CostEstimator
    {
        public class CostInfo
        {
            public int EstimatedInputTokens;
            public int EstimatedOutputTokens;
            public double EstimatedCostRMB;
            public string ModelName;
        }

        // 模型定价（人民币/百万 tokens），约数，用户实际价格以官方为准
        private static readonly Dictionary<string, (double input, double output)> Pricing =
            new Dictionary<string, (double, double)>
            {
                ["deepseek-v4-flash"] = (1.0, 4.0),      // ¥1/M input, ¥4/M output
                ["deepseek-v4-pro"] = (4.0, 16.0),         // ¥4/M input, ¥16/M output
                ["gpt-4o-mini"] = (1.0, 4.0),
                ["gpt-4o"] = (20.0, 80.0),
                ["claude-3-haiku"] = (2.0, 10.0),
                ["claude-3-5-sonnet"] = (22.0, 88.0),
            };

        /// <summary>
        /// 粗略估算 token 数（中文约 1 token/字，英文约 1 token/4 字符）
        /// </summary>
        public static int EstimateTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            int chineseChars = 0;
            int otherChars = 0;
            foreach (char c in text)
            {
                if (c >= 0x4E00 && c <= 0x9FFF) chineseChars++;
                else if (!char.IsWhiteSpace(c)) otherChars++;
            }
            // 英文约 0.25 token/char，中文约 0.6-1 token/char
            return (int)(chineseChars * 0.75 + otherChars * 0.25);
        }

        /// <summary>
        /// 估算一次 AI 分析的费用
        /// </summary>
        public static CostInfo Estimate(string prompt, string modelId, int expectedOutputTokens = 200)
        {
            var inputTokens = EstimateTokens(prompt);
            var info = new CostInfo
            {
                EstimatedInputTokens = inputTokens,
                EstimatedOutputTokens = expectedOutputTokens,
                ModelName = modelId
            };

            if (Pricing.TryGetValue(modelId, out var price))
            {
                double inputCost = (inputTokens / 1000000.0) * price.input;
                double outputCost = (expectedOutputTokens / 1000000.0) * price.output;
                info.EstimatedCostRMB = inputCost + outputCost;
            }
            else
            {
                // 未知模型，按 ¥2/M 估算
                info.EstimatedCostRMB = ((inputTokens + expectedOutputTokens) / 1000000.0) * 2.0;
            }

            return info;
        }

        /// <summary>
        /// 格式化费用为可读字符串
        /// </summary>
        public static string FormatCost(CostInfo info)
        {
            if (info.EstimatedCostRMB < 0.001)
                return $"< RMB0.001 ({info.EstimatedInputTokens} tokens)";
            if (info.EstimatedCostRMB < 0.01)
                return $"~ RMB{info.EstimatedCostRMB:F4} ({info.EstimatedInputTokens} tokens)";
            return $"~ RMB{info.EstimatedCostRMB:F3} ({info.EstimatedInputTokens} tokens)";
        }
    }
}

