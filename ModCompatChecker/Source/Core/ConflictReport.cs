using System.Collections.Generic;

namespace ModCompatChecker.Core
{
    public enum ConflictRisk
    {
        Low,
        Medium,
        High
    }

    public enum HarmonyPatchType
    {
        Prefix,
        Postfix,
        Transpiler,
        Finalizer
    }

    public class HarmonyConflict
    {
        public string ModNameA;
        public string ModPackageIdA;
        public string ModNameB;
        public string ModPackageIdB;
        public string TargetType;
        public string TargetMethod;
        public HarmonyPatchType PatchTypeA;
        public HarmonyPatchType PatchTypeB;
        public ConflictRisk Risk;

        public string Summary { get; set; }

        public bool IsTranspilerConflict =>
            PatchTypeA == HarmonyPatchType.Transpiler || PatchTypeB == HarmonyPatchType.Transpiler;
    }

    public class DefConflict
    {
        public string ModNameA;
        public string ModPackageIdA;
        public string ModNameB;
        public string ModPackageIdB;
        public string DefType;
        public string DefName;
        public string XPathA;
        public string XPathB;
        public ConflictRisk Risk;

        public string Summary { get; set; }
    }

    public class DependencyIssue
    {
        public enum IssueType
        {
            MissingDependency,
            VersionMismatch,
            LoadOrderWarning
        }

        public string ModName;
        public string ModPackageId;
        public string RelatedPackageId;
        public string ExtraInfo;
        public IssueType Type;
        public ConflictRisk Risk;

        public string Summary { get; set; }
    }

    public class ConflictReport
    {
        public List<HarmonyConflict> HarmonyConflicts = new List<HarmonyConflict>();
        public List<DefConflict> DefConflicts = new List<DefConflict>();
        public List<DependencyIssue> DependencyIssues = new List<DependencyIssue>();
        public int TotalLoadedMods;
        public int TotalScannedAssemblies;
        public int TotalScannedPatches;

        public int TotalConflictCount =>
            HarmonyConflicts.Count + DefConflicts.Count + DependencyIssues.Count;

        public bool HasConflicts => TotalConflictCount > 0;
    }
}
