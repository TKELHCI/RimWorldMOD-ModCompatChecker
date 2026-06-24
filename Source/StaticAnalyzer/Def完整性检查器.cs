using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Verse;

namespace ModCompatChecker.StaticAnalyzer
{
    /// <summary>
    /// Def完整性检查器：扫描所有mod的Defs XML，检查引用断链和XML格式错误
    /// 比喻：对照施工队的零件清单（Defs），核对每个引用的零件是否存在
    /// </summary>
    public static class Def完整性检查器
    {
        public class Def问题
        {
            public string Mod名;
            public string 描述;
            public string 位置; // 文件路径:行号
            public bool 是致命;   // XML格式错误=致命, 引用断链=危险
        }

        /// <summary>
        /// 主入口：扫描所有mod的Defs XML
        /// </summary>
        public static List<Def问题> 检查(List<ModContentPack> mods)
        {
            var 问题列表 = new List<Def问题>();

            // 第一步：建全市零件总目录 —— 收集所有mod定义的所有defName
            var 全市Def目录 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var 所有Xml文件 = new List<(string mod名, string xml路径)>();

            Func<ModContentPack, bool> 是Core = m => m.PackageId != null && m.PackageId.StartsWith("ludeon.", StringComparison.OrdinalIgnoreCase);
            foreach (var mod in mods)
            {
                string mod名 = mod.Name;
                bool 跳过此Mod = 是Core(mod);
                string defsDir = Path.Combine(mod.RootDir, "Defs");
                if (!Directory.Exists(defsDir)) continue;

                foreach (var xml文件 in Directory.GetFiles(defsDir, "*.xml", SearchOption.AllDirectories))
                {
                    if (!跳过此Mod) 所有Xml文件.Add((mod名, xml文件));
                    try { 收集DefNames(xml文件, mod名, 全市Def目录); }
                    catch { /* 格式错交给后续处理 */ }
                }

                // 也扫 Patches 文件夹（有些mod把Def定义放在这里）
                string patchesDir = Path.Combine(mod.RootDir, "Patches");
                if (Directory.Exists(patchesDir))
                {
                    foreach (var xml文件 in Directory.GetFiles(patchesDir, "*.xml", SearchOption.AllDirectories))
                    {
                        if (!跳过此Mod) 所有Xml文件.Add((mod名, xml文件));
                        try { 收集DefNames(xml文件, mod名, 全市Def目录); }
                        catch { }
                    }
                }
            }

            // 第二步：逐文件检查引用完整性 + XML格式
            foreach (var (mod名, xml路径) in 所有Xml文件)
            {
                try { 检查XML格式(xml路径, mod名, 问题列表); }
                catch { }

                try { 检查引用完整性(xml路径, mod名, 全市Def目录, 问题列表); }
                catch (Exception ex)
                {
                    问题列表.Add(new Def问题
                    {
                        Mod名 = mod名,
                        描述 = $"解析XML时出错: {ex.Message}",
                        位置 = xml路径,
                        是致命 = true
                    });
                }
            }

            return 问题列表;
        }

        /// <summary>
        /// 从单个XML提取所有 defName → 存入目录
        /// </summary>
        private static void 收集DefNames(string xml路径, string mod名, Dictionary<string, string> 目录)
        {
            using (var reader = XmlReader.Create(xml路径,
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, IgnoreWhitespace = true }))
            {
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == "defName")
                    {
                        string defName = reader.ReadElementContentAsString();
                        if (!string.IsNullOrEmpty(defName) && !目录.ContainsKey(defName))
                            目录[defName] = mod名;
                    }
                }
            }
        }

        /// <summary>
        /// 检查XML是否格式正确（标签闭合、特殊字符转义等）
        /// </summary>
        private static void 检查XML格式(string xml路径, string mod名, List<Def问题> 问题列表)
        {
            try
            {
                using (var reader = XmlReader.Create(xml路径,
                    new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore }))
                {
                    while (reader.Read()) { }
                }
            }
            catch (XmlException ex)
            {
                问题列表.Add(new Def问题
                {
                    Mod名 = mod名,
                    描述 = $"XML格式错误: {ex.Message}",
                    位置 = $"{xml路径} (行{ex.LineNumber})",
                    是致命 = true
                });
            }
        }

        /// <summary>
        /// 检查XML中的Def引用是否存在（断链检测）
        /// 比喻：图纸上说"用零件A"→去全市总目录查有没有这个零件
        /// </summary>
        private static void 检查引用完整性(string xml路径, string mod名,
            Dictionary<string, string> 全市Def目录, List<Def问题> 问题列表)
        {
            // 常见的Def引用属性名
            string[] 引用属性名 = {
                "def", "ParentName", "hediffDef", "thingDef",
                "recipeDef", "researchDef", "soundDef"
            };

            var doc = new XmlDocument();
            doc.Load(xml路径);

            foreach (string 属性名 in 引用属性名)
            {
                var 节点们 = doc.SelectNodes("//*[@" + 属性名 + "]");
                if (节点们 == null) continue;

                foreach (XmlNode 节点 in 节点们)
                {
                    string 引用值 = 节点.Attributes[属性名]?.Value;
                    if (string.IsNullOrEmpty(引用值)) continue;

                    // 跳过明显是类名/命名空间的（包含.的一般是C#类型名，不是defName）
                    if (引用值.Contains(".")) continue;
                    // 跳过原版Def（以特定前缀开头或纯数字）
                    if (跳过原版Def(引用值)) continue;
                    // 查目录
                    if (!全市Def目录.ContainsKey(引用值))
                    {
                        问题列表.Add(new Def问题
                        {
                            Mod名 = mod名,
                            描述 = $"引用了不存在的Def \"{引用值}\"（属性: {属性名}，节点: {节点.Name}）",
                            位置 = xml路径,
                            是致命 = false
                        });
                    }
                }
            }
        }

        /// <summary>
        /// 判断是否是原版Def（不需要跨mod检查的）
        /// </summary>
        private static bool 跳过原版Def(string defName)
        {
            if (defName.Length <= 2) return true;
            if (char.IsDigit(defName[0])) return true;
            return false;
        }
    }
}