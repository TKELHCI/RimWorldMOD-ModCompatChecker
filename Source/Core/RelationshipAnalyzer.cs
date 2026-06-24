using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ModCompatChecker.Core
{
    public class ModRelationship
    {
        public string ModName;
        public string PackageId;
        public List<string> DependsOn = new List<string>();          // What this mod needs
        public List<string> DependedBy = new List<string>();        // Who needs this mod
        public List<string> HarmonyConflictsWith = new List<string>(); // Harmony conflicts
        public List<string> DefConflictsWith = new List<string>();    // Def conflicts
        public List<string> LoadBefore = new List<string>();         // Should load before these
        public List<string> LoadAfter = new List<string>();          // Should load after these
        public List<string> MissingDeps = new List<string>();        // Missing dependencies
        public List<string> VersionIssues = new List<string>();      // Version mismatches
    }

    public static class RelationshipAnalyzer
    {
        /// <summary>
        /// Build a full relationship graph for a specific mod based on the conflict report.
        /// </summary>
        public static ModRelationship Analyze(string packageId, ConflictReport report)
        {
            var rel = new ModRelationship();
            var mod = LoadedModManager.RunningMods.FirstOrDefault(m => m.PackageId == packageId);
            rel.PackageId = packageId;
            rel.ModName = mod?.Name ?? packageId;

            // 1. Dependencies from About.xml and LoadFolders
            if (mod != null)
            {
                // Direct dependencies
                foreach (var other in LoadedModManager.RunningMods)
                {
                    if (other.PackageId == packageId) continue;
                    try
                    {
                        // Check if other mod declares dependency on us
                        var aboutPath = System.IO.Path.Combine(other.RootDir, "About", "About.xml");
                        if (System.IO.File.Exists(aboutPath))
                        {
                            var xml = new System.Xml.XmlDocument();
                            xml.Load(aboutPath);
                            var deps = xml.SelectNodes("//modDependencies/li/packageId");
                            if (deps != null)
                            {
                                foreach (System.Xml.XmlNode dep in deps)
                                {
                                    if (dep.InnerText.Trim() == packageId)
                                        rel.DependedBy.Add(other.Name);
                                }
                            }
                            // Check load order
                            var loadAfter = xml.SelectNodes("//loadAfter/li");
                            if (loadAfter != null)
                            {
                                foreach (System.Xml.XmlNode la in loadAfter)
                                {
                                    if (la.InnerText.Trim() == packageId)
                                        rel.LoadBefore.Add(other.Name);
                                }
                            }
                        }
                    }
                    catch { }
                }
            }

            // 2. Harmony conflicts
            foreach (var hc in report.HarmonyConflicts)
            {
                if (hc.ModPackageIdA == packageId)
                    rel.HarmonyConflictsWith.Add(hc.ModNameB + " (" + hc.TargetMethod + ")");
                else if (hc.ModPackageIdB == packageId)
                    rel.HarmonyConflictsWith.Add(hc.ModNameA + " (" + hc.TargetMethod + ")");
            }

            // 3. Def conflicts
            foreach (var dc in report.DefConflicts)
            {
                if (dc.ModPackageIdA == packageId)
                    rel.DefConflictsWith.Add(dc.ModNameB + " (" + dc.DefName + ")");
                else if (dc.ModPackageIdB == packageId)
                    rel.DefConflictsWith.Add(dc.ModNameA + " (" + dc.DefName + ")");
            }

            // 4. Dependency issues
            foreach (var di in report.DependencyIssues)
            {
                if (di.ModPackageId == packageId)
                {
                    if (di.Type == DependencyIssue.IssueType.MissingDependency)
                        rel.MissingDeps.Add(di.RelatedPackageId ?? "Unknown");
                    else if (di.Type == DependencyIssue.IssueType.VersionMismatch)
                        rel.VersionIssues.Add(di.ExtraInfo ?? di.Summary ?? "Version mismatch");
                    else if (di.Type == DependencyIssue.IssueType.LoadOrderWarning)
                    {
                        if (!string.IsNullOrEmpty(di.RelatedPackageId))
                            rel.LoadAfter.Add(di.RelatedPackageId);
                    }
                }
                else if (di.RelatedPackageId == packageId && di.Type == DependencyIssue.IssueType.MissingDependency)
                {
                    rel.DependedBy.Add(di.ModName + " (missing us)");
                }
            }

            return rel;
        }

        /// <summary>
        /// Get all mods that have conflicts in the report (for the relationship picker).
        /// </summary>
        public static HashSet<string> GetAllConflictingMods(ConflictReport report)
        {
            var set = new HashSet<string>();
            foreach (var hc in report.HarmonyConflicts)
            {
                set.Add(hc.ModNameA); set.Add(hc.ModNameB);
                if (!string.IsNullOrEmpty(hc.ModPackageIdA)) set.Add(hc.ModPackageIdA);
                if (!string.IsNullOrEmpty(hc.ModPackageIdB)) set.Add(hc.ModPackageIdB);
            }
            foreach (var dc in report.DefConflicts)
            {
                set.Add(dc.ModNameA); set.Add(dc.ModNameB);
                if (!string.IsNullOrEmpty(dc.ModPackageIdA)) set.Add(dc.ModPackageIdA);
                if (!string.IsNullOrEmpty(dc.ModPackageIdB)) set.Add(dc.ModPackageIdB);
            }
            foreach (var di in report.DependencyIssues)
            {
                set.Add(di.ModName);
                if (!string.IsNullOrEmpty(di.ModPackageId)) set.Add(di.ModPackageId);
                if (!string.IsNullOrEmpty(di.RelatedPackageId)) set.Add(di.RelatedPackageId);
            }
            return set;
        }
    }
}