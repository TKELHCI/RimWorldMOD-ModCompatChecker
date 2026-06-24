using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using Verse;

namespace ModCompatChecker.UI
{
    /// <summary>
    /// 缁撴灉娓叉煋锛氬叧閿瘝楂樹寒 + 鍙姌鍙犲尯鍩?
    /// </summary>
    public static class ResultRenderer
    {
        private static readonly Color KeywordException = new Color(1f, 0.35f, 0.35f);
        private static readonly Color KeywordMod = new Color(0.4f, 0.8f, 1f);
        private static readonly Color KeywordPath = new Color(0.6f, 0.9f, 0.6f);
        private static readonly Color KeywordError = new Color(1f, 0.5f, 0.2f);
        private static readonly Color NormalText = new Color(0.85f, 0.85f, 0.85f);
        private static readonly Color DimText = new Color(0.5f, 0.5f, 0.5f);

        // 鍏抽敭璇?/ 姝ｅ垯 鈫?棰滆壊
        private static readonly List<(Regex pattern, Color color)> HighlightRules =
            new List<(Regex, Color)>
            {
                (new Regex(@"\b(NullReference|InvalidOperation|Argument|KeyNotFound|IndexOutOfRange|MissingMember|TypeLoad|FileNotFound)Exception\b"), KeywordException),                (new Regex(@"(缺少前置|缺少必需|未加载|未找到|依赖缺失|missing dependency|required mod|not loaded|not found)"), KeywordError),
                (new Regex(@"""[^""]+\.dll""|[\w/\\]+\.dll|[\w/\\]+\.xml"), KeywordPath),                (new Regex(@"\b(Error|错误|Exception|异常):"), KeywordError),
                (new Regex(@"\b(Harmony|HugsLib|ModCompatChecker|\w+Mod)\b"), KeywordMod),
            };

        /// <summary>
        /// 娓叉煋甯﹀叧閿瘝楂樹寒鐨勬枃鏈?
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
            // 鎵惧埌鎵€鏈夊叧閿瘝鐨勫尯闂?
            var highlights = FindHighlights(line);

            if (highlights.Count == 0)
            {
                GUI.color = NormalText;
                Widgets.Label(rect, line);
                GUI.color = Color.white;
                return;
            }

            // 鍒嗘娓叉煋
            float x = rect.x;
            int lastEnd = 0;

            foreach (var h in highlights)
            {
                // 鏅€氭枃鏈
                if (h.Start > lastEnd)
                {
                    var normal = line.Substring(lastEnd, h.Start - lastEnd);
                    var nr = new Rect(x, rect.y, Text.CalcSize(normal).x + 4f, rect.height);
                    GUI.color = NormalText;
                    Widgets.Label(nr, normal);
                    x = nr.xMax; if (x > rect.xMax - 10f) break;
                }

                // 楂樹寒娈?
                var highlighted = line.Substring(h.Start, h.End - h.Start);
                var hr = new Rect(x, rect.y, Text.CalcSize(highlighted).x + 4f, rect.height);
                GUI.color = h.Color;
                Widgets.Label(hr, highlighted);
                x = hr.xMax; if (x > rect.xMax - 10f) { lastEnd = h.End; break; }

                lastEnd = h.End;
            }

            // 鍓╀綑鏅€氭枃鏈?
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
                    // 妫€鏌ユ槸鍚︿笌宸叉湁鍖洪棿閲嶅彔
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

            // 鎸変綅缃帓搴?
            spans.Sort((a, b) => a.Start.CompareTo(b.Start));
            return spans;
        }

        /// <summary>
        /// 鍙姌鍙犲尯鍩燂細杩斿洖鏄惁灞曞紑
        /// </summary>
        public static bool DrawCollapsibleSection(Listing_Standard listing, ref bool expanded,
            string title, float contentHeight, Action drawContent)
        {
            var headerRect = listing.GetRect(28f);
            GUI.color = expanded ? new Color(0.25f, 0.5f, 0.25f) : new Color(0.48f, 0.48f, 0.52f);
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
