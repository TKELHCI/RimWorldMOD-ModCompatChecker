using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Verse;

namespace ModCompatChecker.Core
{
    public static class HarmonyAnalyzer
    {
        private static readonly HashSet<string> HarmonyPatchAttrNames = new HashSet<string>
        {
            "HarmonyLib.HarmonyPatch",
            "HarmonyPatch"
        };

        public static List<HarmonyConflict> Analyze(List<ModContentPack> mods)
        {
            var allPatches = new List<RawPatchEntry>();
            int scannedAssemblies = 0;

            foreach (var mod in mods)
            {
                var modDir = mod.RootDir;
                var assembliesDir = Path.Combine(modDir, "Assemblies");
                if (!Directory.Exists(assembliesDir)) continue;

                var versionDir = Path.Combine(modDir, "v1.6", "Assemblies");
                var dirsToCheck = new List<string> { assembliesDir };
                if (Directory.Exists(versionDir)) dirsToCheck.Add(versionDir);

                foreach (var dir in dirsToCheck)
                {
                    foreach (var dllPath in Directory.GetFiles(dir, "*.dll"))
                    {
                        try
                        {
                            var patches = ScanAssembly(dllPath, mod.Name, mod.PackageId);
                            allPatches.AddRange(patches);
                            scannedAssemblies++;
                        }
                        catch (Exception ex)
                        {
                            Log.Warning($"[ModCompatChecker] Failed to scan {dllPath}: {ex.Message}");
                        }
                    }
                }
            }

            var conflicts = new List<HarmonyConflict>();
            var groups = allPatches
                .GroupBy(p => (p.TargetType, p.TargetMethod))
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

                        var risk = ConflictRisk.Low;
                        if (a.PatchType == HarmonyPatchType.Transpiler || b.PatchType == HarmonyPatchType.Transpiler)
                            risk = ConflictRisk.High;
                        else if (a.PatchType == HarmonyPatchType.Prefix && b.PatchType == HarmonyPatchType.Prefix)
                            risk = ConflictRisk.Medium;

                        conflicts.Add(new HarmonyConflict
                        {
                            ModNameA = a.ModName,
                            ModPackageIdA = a.PackageId,
                            ModNameB = b.ModName,
                            ModPackageIdB = b.PackageId,
                            TargetType = a.TargetType,
                            TargetMethod = a.TargetMethod,
                            PatchTypeA = a.PatchType,
                            PatchTypeB = b.PatchType,
                            Risk = risk,
                            Summary = string.Format("ModCompatChecker.HarmonyConflictSummary".Translate(), a.ModName, b.ModName, a.TargetType, a.TargetMethod)
                        });
                    }
            }

            Log.Message($"[ModCompatChecker] Harmony scan: {scannedAssemblies} assemblies, {allPatches.Count} patches, {conflicts.Count} conflicts");
            return conflicts;
        }

        private class RawPatchEntry
        {
            public string ModName, PackageId, TargetType, TargetMethod;
            public HarmonyPatchType PatchType;
        }

        private static List<RawPatchEntry> ScanAssembly(string dllPath, string modName, string packageId)
        {
            var results = new List<RawPatchEntry>();
            var fileName = Path.GetFileName(dllPath);
            if (fileName == "0Harmony.dll" || fileName == "HugsLib.dll" || fileName == "Mono.Cecil.dll" ||
                fileName.StartsWith("System.") || fileName.StartsWith("UnityEngine") || fileName.StartsWith("Unity."))
                return results;

            using (var assembly = AssemblyDefinition.ReadAssembly(dllPath))
            {
                foreach (var type in assembly.MainModule.Types)
                {
                    var patchAttr = type.CustomAttributes.FirstOrDefault(a =>
                        HarmonyPatchAttrNames.Contains(a.AttributeType.FullName));
                    if (patchAttr == null) continue;

                    string targetType = null;
                    string targetMethod = null;
                    var ctorArgs = patchAttr.ConstructorArguments;

                    if (ctorArgs.Count >= 1)
                        targetType = ResolveTypeArgument(ctorArgs[0]);
                    if (ctorArgs.Count >= 2)
                        targetMethod = ctorArgs[1].Value?.ToString();

                    if (string.IsNullOrEmpty(targetType))
                    {
                        var parts = type.Name.Split('_');
                        if (parts.Length >= 3)
                        {
                            targetType = string.Join(".", parts.Skip(1).Take(parts.Length - 1));
                            targetMethod = parts[parts.Length - 1];
                        }
                        else
                        {
                            targetType = type.Name;
                        }
                    }
                    if (string.IsNullOrEmpty(targetMethod))
                        targetMethod = "*";

                    foreach (var prop in patchAttr.Properties)
                        if (prop.Name == "MethodType" && prop.Argument.Value != null)
                            targetMethod = prop.Argument.Value.ToString();

                    foreach (var method in type.Methods)
                    {
                        HarmonyPatchType? found = null;
                        foreach (var attr in method.CustomAttributes)
                        {
                            var an = attr.AttributeType.FullName;
                            if (an.Contains("HarmonyPrefix")) found = HarmonyPatchType.Prefix;
                            else if (an.Contains("HarmonyPostfix")) found = HarmonyPatchType.Postfix;
                            else if (an.Contains("HarmonyTranspiler")) found = HarmonyPatchType.Transpiler;
                            else if (an.Contains("HarmonyFinalizer")) found = HarmonyPatchType.Finalizer;
                        }
                        if (found.HasValue)
                            results.Add(new RawPatchEntry { ModName = modName, PackageId = packageId, TargetType = targetType, TargetMethod = targetMethod, PatchType = found.Value });
                    }

                    if (!results.Any(r => r.PackageId == packageId && r.TargetType == targetType))
                        results.Add(new RawPatchEntry { ModName = modName, PackageId = packageId, TargetType = targetType, TargetMethod = targetMethod, PatchType = HarmonyPatchType.Prefix });
                }
            }
            return results;
        }

        private static string ResolveTypeArgument(CustomAttributeArgument arg)
        {
            if (arg.Value is TypeReference typeRef) return typeRef.FullName;
            return arg.Value?.ToString();
        }
    }
}
