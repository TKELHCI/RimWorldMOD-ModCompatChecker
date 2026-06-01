using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Verse;

namespace ModCompatChecker.Core
{
    public static class DependencyChecker
    {
        private const string CurrentGameVersion = "1.6";

        public static List<DependencyIssue> Analyze(List<ModContentPack> mods)
        {
            var issues = new List<DependencyIssue>();
            var loadedPkgIds = new HashSet<string>(mods.Select(m => m.PackageId.ToLowerInvariant()));

            for (int idx = 0; idx < mods.Count; idx++)
            {
                var mod = mods[idx];
                var aboutXmlPath = Path.Combine(mod.RootDir, "About", "About.xml");
                if (!File.Exists(aboutXmlPath)) continue;

                try
                {
                    var aboutDoc = new XmlDocument();
                    aboutDoc.Load(aboutXmlPath);
                    CheckModDependencies(aboutDoc, mod.Name, mod.PackageId, loadedPkgIds, issues);
                    CheckVersionCompatibility(aboutDoc, mod.Name, mod.PackageId, issues);
                    CheckLoadOrder(aboutDoc, mod.Name, mod.PackageId, loadedPkgIds, idx, mods, issues);
                }
                catch (Exception ex)
                {
                    Log.Warning($"[ModCompatChecker] Failed to parse About.xml for {mod.Name}: {ex.Message}");
                }
            }

            Log.Message($"[ModCompatChecker] Dependency check: {issues.Count} issues");
            return issues;
        }

        private static void CheckModDependencies(XmlDocument aboutDoc, string modName, string packageId,
            HashSet<string> loadedPkgIds, List<DependencyIssue> issues)
        {
            var depNodes = aboutDoc.SelectNodes("//modDependencies/li");
            if (depNodes == null) return;

            foreach (XmlNode depNode in depNodes)
            {
                var depPkgId = depNode.SelectSingleNode("packageId")?.InnerText?.ToLowerInvariant();
                var depDisplay = depNode.SelectSingleNode("displayName")?.InnerText ?? depPkgId;
                if (string.IsNullOrEmpty(depPkgId)) continue;

                if (!loadedPkgIds.Contains(depPkgId))
                    issues.Add(new DependencyIssue
                    {
                        ModName = modName, ModPackageId = packageId,
                        RelatedPackageId = depDisplay,
                        Type = DependencyIssue.IssueType.MissingDependency,
                        Risk = ConflictRisk.High,
                        Summary = $"{modName} 依赖 {depDisplay}，但后者未加载"
                    });
            }
        }

        private static void CheckVersionCompatibility(XmlDocument aboutDoc, string modName, string packageId,
            List<DependencyIssue> issues)
        {
            var verNodes = aboutDoc.SelectNodes("//supportedVersions/li");
            if (verNodes == null || verNodes.Count == 0) return;

            var supported = new List<string>();
            foreach (XmlNode vn in verNodes) supported.Add(vn.InnerText.Trim());

            bool compat = supported.Any(v =>
            {
                if (v == CurrentGameVersion) return true;
                if (v.Contains("-"))
                {
                    var parts = v.Split('-');
                    if (parts.Length == 2)
                        return CompareVersion(parts[0].Trim(), CurrentGameVersion) <= 0 &&
                               CompareVersion(parts[1].Trim(), CurrentGameVersion) >= 0;
                }
                return false;
            });

            if (!compat)
                issues.Add(new DependencyIssue
                {
                    ModName = modName, ModPackageId = packageId,
                    ExtraInfo = string.Join(", ", supported),
                    Type = DependencyIssue.IssueType.VersionMismatch,
                    Risk = ConflictRisk.High,
                    Summary = $"{modName} 要求游戏版本 {string.Join(",", supported)}，当前为 {CurrentGameVersion}"
                });
        }

        private static void CheckLoadOrder(XmlDocument aboutDoc, string modName, string packageId,
            HashSet<string> loadedPkgIds, int currentIdx, List<ModContentPack> allMods,
            List<DependencyIssue> issues)
        {
            var loadAfterNodes = aboutDoc.SelectNodes("//loadAfter/li");
            if (loadAfterNodes != null)
                foreach (XmlNode node in loadAfterNodes)
                {
                    var req = node.InnerText.Trim().ToLowerInvariant();
                    if (!loadedPkgIds.Contains(req)) continue;
                    var afterIdx = allMods.FindIndex(m => m.PackageId.ToLowerInvariant() == req);
                    if (afterIdx >= 0 && currentIdx < afterIdx)
                        issues.Add(new DependencyIssue
                        {
                            ModName = modName, ModPackageId = packageId,
                            RelatedPackageId = node.InnerText.Trim(),
                            Type = DependencyIssue.IssueType.LoadOrderWarning,
                            Risk = ConflictRisk.Medium,
                            Summary = $"{modName} 应在 {node.InnerText.Trim()} 之后加载"
                        });
                }

            var loadBeforeNodes = aboutDoc.SelectNodes("//loadBefore/li");
            if (loadBeforeNodes != null)
                foreach (XmlNode node in loadBeforeNodes)
                {
                    var req = node.InnerText.Trim().ToLowerInvariant();
                    if (!loadedPkgIds.Contains(req)) continue;
                    var beforeIdx = allMods.FindIndex(m => m.PackageId.ToLowerInvariant() == req);
                    if (beforeIdx >= 0 && currentIdx > beforeIdx)
                        issues.Add(new DependencyIssue
                        {
                            ModName = modName, ModPackageId = packageId,
                            RelatedPackageId = node.InnerText.Trim(),
                            Type = DependencyIssue.IssueType.LoadOrderWarning,
                            Risk = ConflictRisk.Medium,
                            Summary = $"{modName} 应在 {node.InnerText.Trim()} 之前加载"
                        });
                }
        }

        private static int CompareVersion(string a, string b)
        {
            var aParts = a.Split(new char[] { '.' });
            var bParts = b.Split(new char[] { '.' });
            int maxLen = System.Math.Max(aParts.Length, bParts.Length);
            for (int i = 0; i < maxLen; i++)
            {
                int av = i < aParts.Length && int.TryParse(aParts[i], out var x) ? x : 0;
                int bv = i < bParts.Length && int.TryParse(bParts[i], out var y) ? y : 0;
                if (av != bv) return av.CompareTo(bv);
            }
            return 0;
        }
    }
}