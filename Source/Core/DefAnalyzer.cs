using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Verse;

namespace ModCompatChecker.Core
{
    public static class DefAnalyzer
    {
        public static List<DefConflict> Analyze(List<ModContentPack> mods)
        {
            var allPatches = new List<RawDefPatch>();
            int scannedPatchFilesCnt = 0;

            foreach (var mod in mods)
            {
                var patchesDir = Path.Combine(mod.RootDir, "Patches");
                if (!Directory.Exists(patchesDir)) continue;

                foreach (var xmlFile in Directory.GetFiles(patchesDir, "*.xml", SearchOption.AllDirectories))
                {
                    try
                    {
                        var patches = ScanPatchFile(xmlFile, mod.Name, mod.PackageId);
                        allPatches.AddRange(patches);
                        scannedPatchFilesCnt++;
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[ModCompatChecker] Failed to parse {xmlFile}: {ex.Message}");
                    }
                }
            }

            var conflicts = new List<DefConflict>();
            var groups = allPatches
                .GroupBy(p => (p.DefType, p.DefName, NormalizeXPath(p.XPath)))
                .Where(g => g.Select(x => x.PackageId).Distinct().Count() > 1);

            foreach (var group in groups)
            {
                var distinctMods = group
                    .GroupBy(p => p.PackageId)
                    .Select(g => g.First())
                    .ToList();

                for (int i = 0; i < distinctMods.Count; i++)
                    for (int j = i + 1; j < distinctMods.Count; j++)
                    {
                        var a = distinctMods[i];
                        var b = distinctMods[j];
                        var risk = a.XPath == b.XPath ? ConflictRisk.High : ConflictRisk.Medium;

                        conflicts.Add(new DefConflict
                        {
                            ModNameA = a.ModName,
                            ModPackageIdA = a.PackageId,
                            ModNameB = b.ModName,
                            ModPackageIdB = b.PackageId,
                            DefType = a.DefType,
                            DefName = a.DefName,
                            XPathA = a.XPath,
                            XPathB = b.XPath,
                            Risk = risk,
                            Summary = $"{a.ModName} 与 {b.ModName} 修改同一个 Def: {a.DefType}/{a.DefName}"
                        });
                    }
            }

            Log.Message($"[ModCompatChecker] Def scan: {scannedPatchFilesCnt} patch files, {allPatches.Count} xpath ops, {conflicts.Count} conflicts");
            return conflicts;
        }

        private class RawDefPatch
        {
            public string ModName, PackageId, DefType, DefName, XPath;
        }

        private static List<RawDefPatch> ScanPatchFile(string xmlPath, string modName, string packageId)
        {
            var results = new List<RawDefPatch>();
            var doc = new XmlDocument();
            doc.Load(xmlPath);
            var patchOps = doc.SelectNodes("//Patch");
            if (patchOps == null) return results;

            foreach (XmlNode patch in patchOps)
            {
                foreach (XmlNode operation in patch.ChildNodes)
                {
                    if (operation.Name == "#comment") continue;
                    var xpath = operation.Attributes?["xpath"]?.Value;
                    if (string.IsNullOrEmpty(xpath)) continue;

                    var defInfo = ExtractDefInfo(xpath);
                    if (defInfo == null) continue;

                    results.Add(new RawDefPatch
                    {
                        ModName = modName, PackageId = packageId,
                        DefType = defInfo.Item1, DefName = defInfo.Item2, XPath = xpath
                    });
                }
            }
            return results;
        }

        private static Tuple<string, string> ExtractDefInfo(string xpath)
        {
            var defTypeMatch = System.Text.RegularExpressions.Regex.Match(xpath, @"Defs/(\w+Def)\[");
            if (!defTypeMatch.Success) return null;
            var defNameMatch = System.Text.RegularExpressions.Regex.Match(xpath, @"defName\s*=\s*""([^""]+)""");
            return Tuple.Create(defTypeMatch.Groups[1].Value,
                defNameMatch.Success ? defNameMatch.Groups[1].Value : "*");
        }

        private static string NormalizeXPath(string xpath)
        {
            return xpath.Trim().Replace(" ", "").Replace("\t", "").Replace("\r", "").Replace("\n", "");
        }
    }
}
