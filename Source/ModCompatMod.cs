using UnityEngine;
using Verse;

namespace ModCompatChecker
{
    public class ModCompatMod : Mod
    {
        public static ModCompatMod Instance { get; private set; }
        public ModCompatSettings Settings { get; private set; }

        public ModCompatMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<ModCompatSettings>();
            Log.Message("[ModCompatChecker] Mod Compatibility Checker v1.6 loaded.");
        }

        public override string SettingsCategory()
        {
            return "ModCompatChecker.SettingsCategory".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoSettingsWindowContents(inRect);
        }

        public override void WriteSettings()
        {
            base.WriteSettings();        }
    }
}
