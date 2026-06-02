using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Verse;

namespace ModCompatChecker.Core
{
    public class StackTraceMatch
    {
        public string ModName;
        public string ModPackageId;
        public string AssemblyName;
        public string MatchedType;
        public string MatchedMethod;
        public string PatchType; // null, "Prefix", "Postfix", "Transpiler", "Finalizer"
    }

    public static class StackTraceAnalyzer
    {
        private static readonly string[] VanillaNamespaces = { "Verse.", "RimWorld.", "UnityEngine.", "Unity.", "System.", "Mono.", "Microsoft.", "Ludeon." };
        private static readonly string[] HarmonyTypes = { "HarmonyPrefix", "HarmonyPostfix", "HarmonyTranspiler", "HarmonyFinalizer" };

        private static readonly Regex AtFrameRegex = new Regex(
            @"at\s+(\S+?)\.(\S+?)\.(\S+?)\s*\(", RegexOptions.Compiled);
        private static readonly Regex HarmonyFrameRegex = new Regex(
            @"-\s+(PREFIX|POSTFIX|TRANSPILER|FINALIZER)\s+(\S+?):\s+(\S+?)\.(\S+?):(\S+?)\(",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex CtorFrameRegex = new Regex(
            @"^(\S+?)\.(\S+?)\.\.ctor\s*\(\)", RegexOptions.Compiled | RegexOptions.Multiline);

        public class ExtractedFrame
        {
            public string Namespace;
            public string ClassName;
            public string MethodName;
            public string PatchType; // from Harmony frames
            public string ModHint;   // from Harmony frames: the mod identifier
            public bool IsHarmonyFrame;
        }

        public static List<ExtractedFrame> ExtractFrames(string stackTrace)
        {
            var frames = new List<ExtractedFrame>();

            // 1. Harmony patch frames
            foreach (Match m in HarmonyFrameRegex.Matches(stackTrace))
            {
                frames.Add(new ExtractedFrame
                {
                    PatchType = m.Groups[1].Value,
                    ModHint = m.Groups[2].Value,
                    Namespace = m.Groups[3].Value,
                    ClassName = m.Groups[4].Value,
                    MethodName = m.Groups[5].Value,
                    IsHarmonyFrame = true
                });
            }

            // 2. Regular "at ..." frames
            foreach (Match m in AtFrameRegex.Matches(stackTrace))
            {
                var ns = m.Groups[1].Value;
                var cls = m.Groups[2].Value;
                var method = m.Groups[3].Value;
                // Filter nested class names
                if (cls.Contains("+"))
                {
                    var parts = cls.Split('+');
                    cls = parts[parts.Length - 1];
                    ns = ns + "." + string.Join(".", parts.Take(parts.Length - 1));
                }
                frames.Add(new ExtractedFrame
                {
                    Namespace = ns,
                    ClassName = cls,
                    MethodName = method,
                    IsHarmonyFrame = false
                });
            }

            // 3. Constructor frames (no "at" prefix)
            foreach (Match m in CtorFrameRegex.Matches(stackTrace))
            {
                frames.Add(new ExtractedFrame
                {
                    Namespace = m.Groups[1].Value,
                    ClassName = m.Groups[2].Value,
                    MethodName = ".ctor"
                });
            }

            return frames;
        }

        public static bool IsVanilla(string fullTypeName)
        {
            foreach (var ns in VanillaNamespaces)
                if (fullTypeName.StartsWith(ns, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        public static List<StackTraceMatch> ScanModAssemblies(List<ExtractedFrame> frames)
        {
            var results = new List<StackTraceMatch>();
            var mods = LoadedModManager.RunningModsListForReading;
            if (mods == null) return results;

            foreach (var frame in frames)
            {
                var fullType = frame.Namespace + "." + frame.ClassName;
                if (IsVanilla(fullType)) continue;

                foreach (var mod in mods)
                {
                    var asmDir = Path.Combine(mod.RootDir, "Assemblies");
                    if (!Directory.Exists(asmDir)) continue;

                    foreach (var dll in Directory.GetFiles(asmDir, "*.dll"))
                    {
                        var fileName = Path.GetFileName(dll);
                        if (fileName == "0Harmony.dll" || fileName == "HugsLib.dll" ||
                            fileName == "Mono.Cecil.dll" || fileName.StartsWith("System."))
                            continue;

                        try
                        {
                            using (var asm = AssemblyDefinition.ReadAssembly(dll))
                            {
                                foreach (var type in asm.MainModule.Types)
                                {
                                    // Match by class name
                                    if (!type.Name.Equals(frame.ClassName, StringComparison.Ordinal) &&
                                        !type.FullName.Equals(fullType, StringComparison.Ordinal))
                                        continue;

                                    // Check for Harmony patch attributes
                                    string patchType = null;
                                    foreach (var attr in type.CustomAttributes)
                                    {
                                        foreach (var ht in HarmonyTypes)
                                        {
                                            if (attr.AttributeType.FullName.Contains(ht))
                                            {
                                                patchType = ht.Replace("Harmony", "");
                                                break;
                                            }
                                        }
                                    }
                                    if (patchType == null)
                                    {
                                        foreach (var method in type.Methods)
                                        {
                                            foreach (var attr in method.CustomAttributes)
                                            {
                                                foreach (var ht in HarmonyTypes)
                                                {
                                                    if (attr.AttributeType.FullName.Contains(ht))
                                                    {
                                                        patchType = ht.Replace("Harmony", "");
                                                        break;
                                                    }
                                                }
                                                if (patchType != null) break;
                                            }
                                            if (patchType != null) break;
                                        }
                                    }

                                    results.Add(new StackTraceMatch
                                    {
                                        ModName = mod.Name,
                                        ModPackageId = mod.PackageId ?? "",
                                        AssemblyName = fileName,
                                        MatchedType = type.FullName,
                                        MatchedMethod = frame.MethodName,
                                        PatchType = patchType
                                    });
                                }
                            }
                        }
                        catch { /* skip unreadable assemblies */ }
                    }
                }

                // Also check Harmony frame mod hints
                if (frame.IsHarmonyFrame && !string.IsNullOrEmpty(frame.ModHint))
                {
                    var matchingMod = mods.FirstOrDefault(m =>
                        (!string.IsNullOrEmpty(m.PackageId) &&
                         frame.ModHint.Contains(m.PackageId.Split('.').Last())) ||
                        m.Name.IndexOf(frame.ModHint, StringComparison.OrdinalIgnoreCase) >= 0);

                    if (matchingMod != null)
                    {
                        results.Add(new StackTraceMatch
                        {
                            ModName = matchingMod.Name,
                            ModPackageId = matchingMod.PackageId ?? "",
                            AssemblyName = "(Harmony patch)",
                            MatchedType = frame.ClassName,
                            MatchedMethod = frame.MethodName,
                            PatchType = frame.PatchType
                        });
                    }
                }
            }

            return results.DistinctBy(r => r.ModPackageId + r.MatchedType + r.MatchedMethod).ToList();
        }

        public static string BuildReport(List<ExtractedFrame> frames, List<StackTraceMatch> matches)
        {
            var sb = new System.Text.StringBuilder();

            // Summary
            var nonVanilla = frames.Where(f => !IsVanilla(f.Namespace + "." + f.ClassName)).ToList();
            sb.AppendLine("──── 堆栈分析 ────");
            sb.AppendLine($"提取 {frames.Count} 帧，其中 {nonVanilla.Count} 帧来自非原版代码");
            sb.AppendLine();

            if (matches.Count == 0)
            {
                sb.AppendLine("✓ 未在 MOD DLL 中找到匹配");
                sb.AppendLine("  崩溃可能来自原版代码或数据冲突");
                return sb.ToString();
            }

            sb.AppendLine($"涉及 {matches.Select(m => m.ModName).Distinct().Count()} 个 MOD:");
            sb.AppendLine();

            var grouped = matches.GroupBy(m => m.ModName);
            foreach (var g in grouped)
            {
                sb.AppendLine($"▌ {g.Key}");
                foreach (var m in g)
                {
                    var line = $"  · {m.MatchedType}.{m.MatchedMethod}";
                    if (!string.IsNullOrEmpty(m.PatchType))
                        line += $" [{m.PatchType}]";
                    sb.AppendLine(line);
                }
                sb.AppendLine();
            }

            // Check for conflicts
            var conflictGroups = matches
                .Where(m => !string.IsNullOrEmpty(m.PatchType))
                .GroupBy(m => m.MatchedType + "." + m.MatchedMethod)
                .Where(g => g.Select(x => x.ModPackageId).Distinct().Count() > 1);

            if (conflictGroups.Any())
            {
                sb.AppendLine("⚠ Harmony 补丁冲突:");
                foreach (var cg in conflictGroups)
                {
                    sb.AppendLine($"  {cg.Key}:");
                    foreach (var m in cg)
                        sb.AppendLine($"    · {m.ModName} [{m.PatchType}]");
                }
            }

            return sb.ToString();
        }
    }

    // LINQ DistinctBy polyfill for .NET 4.7.2
    internal static class LinqExtensions
    {
        public static IEnumerable<TSource> DistinctBy<TSource, TKey>(
            this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            var seen = new HashSet<TKey>();
            foreach (var item in source)
                if (seen.Add(keySelector(item)))
                    yield return item;
        }
    }
}
