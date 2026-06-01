using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using Verse;

namespace ModCompatChecker.UI
{
    /// <summary>
    /// 结果渲染：关键词高亮 + 可折叠区域
    /// </summary>
    public static class ResultRenderer
    {
        private static readonly Color KeywordException = new Color(1f, 0.35f, 0.35f);
        private static readonly Color KeywordMod = new Color(0.4f, 0.8f, 1f);
        private static readonly Color KeywordPath = new Color(0.6f, 0.9f, 0.6f);
        private static readonly Color KeywordError = new Color(1f, 0.5f, 0.2f);
        private static readonly Color NormalText = new Color(0.85f, 0.85f, 0.85f);
        private static readonly Color DimText = new Color(0.5f, 0.5f, 0.5f);

        // 关键词 / 正则 → 颜色
        private static readonly List<(Regex pattern, Color color)> HighlightRules =
            new List<(Regex, Color)>
            {
                (new Regex(@"\b(NullReference|InvalidOperation|Argument|KeyNotFound|IndexOutOfRange|MissingMember|TypeLoad|FileNotFound)Exception\b"), KeywordException),
                (new Regex(@"(缺少前置|缺少必需品|missing dependency|required mod|not loaded|not found|未加载|未找到|依赖缺失)"), KeywordError),
                (new Regex(@"""[^""]+\.dll""|[\w/\\]+\.dll|[\w/\\]+\.xml"), KeywordPath),
                (new Regex(@"\b(Error|错误|Exception|异常):"), KeywordError),
                (new Regex(@"\b(Harmony|HugsLib|ModCompatChecker|\w+Mod)\b"), KeywordMod),
            };

        /// <summary>
        /// 渲染带关键词高亮的文本
        /// </summary>
        public static void RenderHighlighted(Rect rect, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                Widgets.Label(rect, "");
                return;
            }

            var lines = text.Split('\n');
            float y = rect.y;
            float lineHeight = Text.LineHeight;

            foreach (var line in lines)
            {
                if (y > rect.yMax) break;

                var lineRect = new Rect(rect.x, y, rect.width, lineHeight);
                RenderLineHighlighted(lineRect, line);
                y += lineHeight;
            }
        }

        private static void RenderLineHighlighted(Rect rect, string line)
        {
            // 找到所有关键词的区间
            var highlights = FindHighlights(line);

            if (highlights.Count == 0)
            {
                GUI.color = NormalText;
                Widgets.Label(rect, line);
                GUI.color = Color.white;
                return;
            }

            // 分段渲染
            float x = rect.x;
            int lastEnd = 0;

            foreach (var h in highlights)
            {
                // 普通文本段
                if (h.Start > lastEnd)
                {
                    var normal = line.Substring(lastEnd, h.Start - lastEnd);
                    var nr = new Rect(x, rect.y, Text.CalcSize(normal).x + 4f, rect.height);
                    GUI.color = NormalText;
                    Widgets.Label(nr, normal);
                    x = nr.xMax; if (x > rect.xMax - 10f) break;
                }

                // 高亮段
                var highlighted = line.Substring(h.Start, h.End - h.Start);
                var hr = new Rect(x, rect.y, Text.CalcSize(highlighted).x + 4f, rect.height);
                GUI.color = h.Color;
                Widgets.Label(hr, highlighted);
                x = hr.xMax; if (x > rect.xMax - 10f) { lastEnd = h.End; break; }

                lastEnd = h.End;
            }

            // 剩余普通文本
            if (lastEnd < line.Length)
            {
                var rest = line.Substring(lastEnd);
                var rr = new Rect(x, rect.y, Text.CalcSize(rest).x + 4f, rect.height);
                GUI.color = NormalText;
                Widgets.Label(rr, rest);
            }

            GUI.color = Color.white;
        }

        private class HighlightSpan
        {
            public int Start, End;
            public Color Color;
        }

        private static List<HighlightSpan> FindHighlights(string text)
        {
            var spans = new List<HighlightSpan>();

            foreach (var rule in HighlightRules)
            {
                foreach (Match m in rule.pattern.Matches(text))
                {
                    // 检查是否与已有区间重叠
                    bool overlap = false;
                    foreach (var existing in spans)
                        if (m.Index < existing.End && m.Index + m.Length > existing.Start)
                        { overlap = true; break; }

                    if (!overlap)
                        spans.Add(new HighlightSpan
                        {
                            Start = m.Index,
                            End = m.Index + m.Length,
                            Color = rule.color
                        });
                }
            }

            // 按位置排序
            spans.Sort((a, b) => a.Start.CompareTo(b.Start));
            return spans;
        }

        /// <summary>
        /// 可折叠区域：返回是否展开
        /// </summary>
        public static bool DrawCollapsibleSection(Listing_Standard listing, ref bool expanded,
            string title, float contentHeight, Action drawContent)
        {
            var headerRect = listing.GetRect(28f);
            GUI.color = expanded ? new Color(0.25f, 0.5f, 0.25f) : new Color(0.18f, 0.18f, 0.18f);
            Widgets.DrawBoxSolid(headerRect, GUI.color);
            GUI.color = Color.white;

            string arrow = expanded ? "▼" : "▶";
            Widgets.Label(new Rect(headerRect.x + 6f, headerRect.y + 4f, headerRect.width - 12f, 20f),
                $"{arrow}  {title}");

            if (Widgets.ButtonInvisible(headerRect))
                expanded = !expanded;

            if (expanded)
            {
                listing.Gap(4f);
                drawContent();
                listing.Gap(6f);
            }

            return expanded;
        }
    }
}
