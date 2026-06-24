using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Verse;

namespace ModCompatChecker.StaticAnalyzer
{
    /// <summary>
    /// 贴图/音频预检器：检查XML中引用的贴图和音频文件是否真实存在
    /// 比喻：图纸上说"用大理石花纹砖"→去仓库(Textures文件夹)核实有没有
    /// </summary>
    public static class 贴图音频预检器
    {
        public class 资源问题
        {
            public string Mod名;
            public string 引用路径;    // XML里写的路径
            public string 类型;        // "贴图" 或 "音频"
            public string 来源文件;    // 哪个XML引用的
        }

        /// <summary>
        /// 主入口
        /// </summary>
        public static List<资源问题> 检查(List<ModContentPack> mods)
        {
            var 问题列表 = new List<资源问题>();

            foreach (var mod in mods)
            {
                string mod名 = mod.Name;
                string modRoot = mod.RootDir;

                // 清点仓库实际库存
                var 实际文件库存 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                string texturesDir = Path.Combine(modRoot, "Textures");
                if (Directory.Exists(texturesDir))
                {
                    foreach (var f in Directory.GetFiles(texturesDir, "*", SearchOption.AllDirectories))
                    {
                        // 存储相对于Textures文件夹的路径（不含扩展名），忽略大小写
                        string 相对路径 = f.Substring(texturesDir.Length + 1);
                        string 无扩展名 = Path.ChangeExtension(相对路径, null);
                        实际文件库存.Add(无扩展名.Replace("\\", "/"));
                        // 也加原始路径（有的引用带扩展名）
                        实际文件库存.Add(相对路径.Replace("\\", "/"));
                    }
                }

                var 音频库存 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string soundsDir = Path.Combine(modRoot, "Sounds");
                if (Directory.Exists(soundsDir))
                {
                    foreach (var f in Directory.GetFiles(soundsDir, "*", SearchOption.AllDirectories))
                    {
                        string 相对路径 = f.Substring(soundsDir.Length + 1);
                        音频库存.Add(相对路径.Replace("\\", "/"));
                    }
                }

                // 扫描Defs XML找 texPath / audio引用
                string defsDir = Path.Combine(modRoot, "Defs");
                if (!Directory.Exists(defsDir)) continue;

                foreach (var xml文件 in Directory.GetFiles(defsDir, "*.xml", SearchOption.AllDirectories))
                {
                    try
                    {
                        检查贴图音频引用(xml文件, mod名, 实际文件库存, 音频库存, 问题列表);
                    }
                    catch { /* 跳过格式错 */ }
                }
            }

            return 问题列表;
        }

        private static void 检查贴图音频引用(string xml路径, string mod名,
            HashSet<string> 贴图库存, HashSet<string> 音频库存, List<资源问题> 问题列表)
        {
            using (var reader = XmlReader.Create(xml路径,
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, IgnoreWhitespace = true }))
            {
                int _depth = -1;
                string 当前元素 = null;
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.Depth >= _depth)
                    {
                        _depth = reader.Depth;
                        当前元素 = reader.Name;
                    }
                    else if (reader.NodeType == XmlNodeType.Text && 当前元素 != null && reader.Depth == _depth)
                    {
                        string 值 = reader.Value?.Trim();
                        if (string.IsNullOrEmpty(值)) continue;

                        if (当前元素 == "texPath")
                        {
                            if (!贴图库存.Contains(值))
                            {
                                string 无扩展名 = Path.ChangeExtension(值, null);
                                if (!贴图库存.Contains(无扩展名))
                                {
                                    问题列表.Add(new 资源问题
                                    {
                                        Mod名 = mod名,
                                        引用路径 = 值,
                                        类型 = "贴图",
                                        来源文件 = xml路径
                                    });
                                }
                            }
                        }
                        else if (当前元素 == "clipPath" || 当前元素 == "audioPath" || 当前元素 == "soundPath")
                        {
                            if (!音频库存.Contains(值))
                            {
                                // 也尝试去掉扩展名再查（和贴图一样的逻辑）
                                string 无扩展名 = Path.ChangeExtension(值, null);
                                if (!音频库存.Contains(无扩展名))
                                {
                                    问题列表.Add(new 资源问题
                                    {
                                        Mod名 = mod名,
                                        引用路径 = 值,
                                        类型 = "音频",
                                        来源文件 = xml路径
                                    });
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}