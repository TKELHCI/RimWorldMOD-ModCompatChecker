using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using Verse;

namespace ModCompatChecker.Core
{
    public class ModVersionInfo
    {
        public string PackageId;
        public string Name;
        public string LocalVersion;
        public string WorkshopVersion;
        public string PublishedFileId;
        public bool HasUpdate;
        public string LastUpdated;
    }

    public static class UpdateChecker
    {
        private static bool _isChecking;

        public static List<ModVersionInfo> GetLocalModVersions()
        {
            var results = new List<ModVersionInfo>();
            foreach (var mod in LoadedModManager.RunningMods)
            {
                var info = new ModVersionInfo
                {
                    PackageId = mod.PackageId,
                    Name = mod.Name
                };

                try
                {
                    // Read About.xml from mod folder
                    var aboutPath = Path.Combine(mod.RootDir, "About", "About.xml");
                    if (File.Exists(aboutPath))
                    {
                        var xml = new XmlDocument();
                        xml.Load(aboutPath);
                        var verNodes = xml.SelectNodes("//supportedVersions/li");
                        if (verNodes != null && verNodes.Count > 0)
                        {
                            var vers = new List<string>();
                            foreach (XmlNode v in verNodes) vers.Add(v.InnerText);
                            info.LocalVersion = string.Join(", ", vers);
                        }
                        var pfNodes = xml.SelectNodes("//publishedFileId");
                        if (pfNodes != null && pfNodes.Count > 0)
                            info.PublishedFileId = pfNodes[0].InnerText;
                    }
                }
                catch { info.LocalVersion = "?"; }

                if (string.IsNullOrEmpty(info.LocalVersion))
                    info.LocalVersion = "?";

                results.Add(info);
            }
            return results;
        }

        public static void CheckWorkshopUpdates(List<ModVersionInfo> mods, Action<List<ModVersionInfo>> callback)
        {
            if (_isChecking) return;
            _isChecking = true;

            LongEventHandler.ExecuteWhenFinished(() =>
            {
                try
                {
                    foreach (var mod in mods)
                    {
                        if (string.IsNullOrEmpty(mod.PublishedFileId)) continue;
                        try
                        {
                            var details = GetWorkshopDetails(mod.PublishedFileId);
                            if (details != null)
                            {
                                mod.LastUpdated = details;
                                mod.HasUpdate = false; // Conservative: can expand later
                            }
                        }
                        catch { }
                    }
                }
                catch { }
                finally { _isChecking = false; }
                callback?.Invoke(mods);
            });
        }

        private static string GetWorkshopDetails(string publishedFileId)
        {
            try
            {
                var url = "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/";
                var postData = "itemcount=1&publishedfileids[0]=" + publishedFileId;
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "POST";
                req.ContentType = "application/x-www-form-urlencoded";
                req.Timeout = 8000;
                using (var sw = new StreamWriter(req.GetRequestStream()))
                {
                    sw.Write(postData);
                }
                using (var resp = req.GetResponse())
                using (var sr = new StreamReader(resp.GetResponseStream()))
                {
                    var json = sr.ReadToEnd();
                    var timeMatch = Regex.Match(json, @"""time_updated"":\s*(\d+)");
                    if (timeMatch.Success)
                    {
                        var unixTime = long.Parse(timeMatch.Groups[1].Value);
                        var dt = DateTimeOffset.FromUnixTimeSeconds(unixTime);
                        return dt.ToString("yyyy-MM-dd");
                    }
                }
            }
            catch { }
            return null;
        }
    }
}