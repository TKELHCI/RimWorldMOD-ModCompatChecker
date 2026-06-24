using System.Collections.Generic;
using ModCompatChecker.AI;
using UnityEngine;
using Verse;

namespace ModCompatChecker.UI
{
    public class ModCompatLandingWindow : Window
    {
        private readonly SharedSettingsUI.UIState _uiState = new SharedSettingsUI.UIState();

        public override Vector2 InitialSize => new Vector2(580f, 480f);

        public ModCompatLandingWindow()
        {
            doCloseX = true;
            doCloseButton = true;
            closeOnAccept = false;
            absorbInputAroundWindow = true;
            draggable = true;
            resizeable = true;
            layer = WindowLayer.Dialog;
        }

        public override void PreClose() { _uiState.Disposed = true; base.PreClose(); }

        public override void DoWindowContents(Rect inRect)
        {
            var settings = ModCompatChecker.ModCompatMod.Instance?.Settings;
            if (settings == null) return;

            var listing = new Listing_Standard();
            listing.Begin(inRect);

            Text.Font = GameFont.Medium;
            listing.Label("ModCompatChecker.MainTitle".Translate(), -1);
            Text.Font = GameFont.Small;
            listing.Gap(8f);

            SharedSettingsUI.DrawModelSelector(listing, settings, _uiState);
            listing.Gap(10f);
            SharedSettingsUI.DrawAPISettings(listing, settings, _uiState);
            listing.Gap(8f);
            SharedSettingsUI.DrawTestConnection(listing, settings, _uiState);
            listing.Gap(16f);

            GUI.color = new Color(0.3f, 0.55f, 0.3f);
            Widgets.DrawBoxSolid(listing.GetRect(2f), GUI.color);
            GUI.color = Color.white;
            listing.Gap(8f);

            Text.Font = GameFont.Medium;
            listing.Label("ModCompatChecker.Features".Translate(), -1);
            Text.Font = GameFont.Small;
            listing.Gap(6f);

            if (listing.ButtonText("ModCompatChecker.CompatAnalysis".Translate()))
                Find.WindowStack.Add(new UnifiedWindow());
            listing.Gap(4f);

            if (listing.ButtonText("ModCompatChecker.ErrorLogTitle".Translate()))
                Find.WindowStack.Add(new ErrorAnalysisWindow());

            listing.End();
        }
    }
}
