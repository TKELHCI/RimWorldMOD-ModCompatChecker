using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Xml;
using System.Text;
using Verse;
using UnityEngine;


namespace 存档移除mod检查影响器  //草搞半天这玩意可以用中文
{

public static class 存档移除mod检查影响器_FOB

{


    public enum 汇报威胁级别 { 无危胁_绿, 警告_黄, 严重_红 }

    public class 一个发现

    {
        public string 具体物体名称;   //问题物体名

        public string 物体的类别;    //物体类别 也就是像 武器  护甲  食物等的分类

        public 汇报威胁级别 威胁等级;   //威胁等级就是上面的安全_绿 黄 红什么的
    }


//base

    public static (List<一个发现> 所发现的列表,汇报威胁级别 总威胁等级评估)

    BEF_检测存档移除(string 该存档的路径,string mod的Defs也就是定义类型的文件夹路径)

    {
        var 黑名单 = 建立黑名单(mod的Defs也就是定义类型的文件夹路径);
        Log.Message("[存档检测] Defs路径: " + (mod的Defs也就是定义类型的文件夹路径 ?? "null"));
        Log.Message("[存档检测] 黑名单条目数: " + 黑名单.Count);
        if(黑名单.Count > 0 && 黑名单.Count <= 5)
        {
            foreach(var bl in 黑名单) Log.Message("[存档检测]   黑名单样本: " + bl);
        }
        var 空列表 = new List<一个发现>();
        if(黑名单.Count == 0){ Log.Warning("[存档检测] 黑名单为空！"); return (空列表,汇报威胁级别.无危胁_绿); }

        var 所发现的列表 = 搜查存档(该存档的路径,黑名单);

        汇报威胁级别 总体评估 = 计算评估(所发现的列表);

        return (所发现的列表,总体评估);
    }


//1  

    static HashSet<string> 建立黑名单(string Defs文件夹)
    {
        
        var 名单 = new HashSet<string>();
        
        // 尝试找到真正的 Defs 目录（可能在子文件夹如 1.6/Defs/）
        string 真实Defs路径 = Defs文件夹;
        if (!Directory.Exists(真实Defs路径))
        {
            // 从mod根目录搜：找任意包含 Defs 的子目录
            string mod根 = Path.GetDirectoryName(Defs文件夹);
            if (mod根 != null && Directory.Exists(mod根))
            {
                var defsDirs = Directory.GetDirectories(mod根, "Defs", SearchOption.AllDirectories);
                if (defsDirs.Length > 0)
                {
                    真实Defs路径 = defsDirs[0];
                    Log.Message("[存档检测] 自动定位到Defs: " + 真实Defs路径);
                }
            }
        }
        
        if (!Directory.Exists(真实Defs路径)) 
        { 
            Log.Warning("[存档检测] 未找到Defs文件夹，尝试路径: " + Defs文件夹); 
            return 名单; 
        }

        var xmlFiles = Directory.GetFiles(真实Defs路径,"*.xml", SearchOption.AllDirectories);
        Log.Message("[存档检测] Defs路径: " + 真实Defs路径);
        Log.Message("[存档检测] 找到XML文件数: " + xmlFiles.Length);
        foreach (var xml文件 in xmlFiles)
        {
            抽出def文件名字(xml文件, 名单);
        }
        return 名单;


    }

    
//2
    static void 抽出def文件名字(string 文件路径, HashSet<string> 名单)
    {
        int defNameCount = 0;
        using (var reader = XmlReader.Create(文件路径,
            new XmlReaderSettings {IgnoreComments = true, IgnoreWhitespace = true}))
        {
            while(reader.Read())
            {
                if(reader.NodeType == XmlNodeType.Element && reader.Name=="defName")
                {
                    reader.Read();
                    if(reader.NodeType == XmlNodeType.Text)
                    {
                        名单.Add(reader.Value.Trim());
                        defNameCount++;
                    }
                }
            }
        }
        if(defNameCount == 0) Log.Message("[存档检测]   文件无defName: " + System.IO.Path.GetFileName(文件路径));
    }


    static List<一个发现>搜查存档(string 该存档的路径,HashSet<string>黑名单)
    {
        var 所发现的列表=new List<一个发现>();

        try
        {
            using (var fs = File.OpenRead(该存档的路径))
            {
                // 读前两字节判断是否是 gzip（0x1F 0x8B）
                byte[] header = new byte[2];
                int read = fs.Read(header, 0, 2);
                fs.Seek(0, SeekOrigin.Begin);

                XmlReader reader;
                if (read >= 2 && header[0] == 0x1F && header[1] == 0x8B)
                {
                    var gz = new GZipStream(fs, CompressionMode.Decompress);
                    reader = XmlReader.Create(gz,
                        new XmlReaderSettings { IgnoreComments = true, IgnoreWhitespace = true, CloseInput = true });
                }
                else
                {
                    reader = XmlReader.Create(fs,
                        new XmlReaderSettings { IgnoreComments = true, IgnoreWhitespace = true, CloseInput = true });
                }

                using (reader)
                {
                    int _scanCount = 0;
                    int _defMatch = 0;
                    int _listHit = 0;
                    string 当前大区 = "";

                    while (reader.Read())
                    {
                        if(reader.NodeType!=XmlNodeType.Element)continue;
                        _scanCount++;

                        switch(reader.Name)
                        {
                            case "things":    当前大区 = "物品区"; break;
                            case "pawns":     当前大区 = "人物区"; break;
                            case "factions":  当前大区 = "派系区"; break;
                            case "ideos":     当前大区 = "文化区"; break;
                            case "quests":    当前大区 = "任务区"; break;
                            case "world":     当前大区 = "世界区 world"; break;
                            case "maps":      当前大区 = "地图区 maps"; break;
                        }

                        if(reader.Name=="def"||reader.Name=="kindDef"||reader.Name=="hediffDef"||reader.Name=="ideo")
                        {
                            reader.Read();
                            if(reader.NodeType==XmlNodeType.Text)
                            {   
                                _defMatch++;
                                string MH60X_SilentHawk = reader.Value.Trim();
                                if(黑名单.Contains(MH60X_SilentHawk)&&!IsVanillaDef(MH60X_SilentHawk))
                                {
                                    _listHit++;
                                    所发现的列表.Add(new 一个发现
                                    {
                                        具体物体名称 = MH60X_SilentHawk,
                                        物体的类别   = 当前大区,
                                        威胁等级 = 定级别(当前大区)
                                    });
                                }
                            }
                        }
                    }
                    Log.Message("[存档检测] 扫描完成 — 元素:" + _scanCount + " def匹配:" + _defMatch + " 黑名单命中:" + _listHit + " 发现:" + 所发现的列表.Count);
                }
            }
        }
        catch(Exception ex)
        {
            Log.Error("搜查存档失败: " + 该存档的路径 + " - " + ex.Message);
        }

        return 所发现的列表;
    }





    static 汇报威胁级别 定级别(string 大区名)
    {
        if (大区名 == "人物区" || 大区名 == "派系区" ||
            大区名 == "文化区" || 大区名.Contains("世界"))
            return 汇报威胁级别.严重_红;
        return 汇报威胁级别.警告_黄;
    }
    static 汇报威胁级别 计算评估(List<一个发现> 列表)
    {
        if (列表.Count == 0) return 汇报威胁级别.无危胁_绿;
        foreach (var 发现 in 列表)
            if (发现.威胁等级 == 汇报威胁级别.严重_红)
                return 汇报威胁级别.严重_红;
        return 汇报威胁级别.警告_黄;
    }


    static readonly HashSet<string> 原版零件表=new HashSet<string>();

    static bool 原版已加载 = false;

    static bool IsVanillaDef(string defName)
    {
        if(!原版已加载)加载原版零件表();
        return 原版零件表.Contains(defName);
    }

    static void 加载原版零件表()
    {
        string 游戏根目录=Path.Combine(Application.dataPath,"..");
        string coreDefs=Path.Combine(游戏根目录,"Data","Core","Defs");
        if(Directory.Exists(coreDefs))
        {
            foreach(var EF2000 in Directory.GetFiles(coreDefs,"*.xml",SearchOption.AllDirectories))
            {
                抽出def文件名字(EF2000, 原版零件表);
            }
        }
        原版已加载 = true;
    }








}
}