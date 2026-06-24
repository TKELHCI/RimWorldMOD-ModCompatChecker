using UnityEngine;
using Verse;
using RimWorld;

namespace ModCompatChecker
{
    [StaticConstructorOnStartup]
    public class ModCompatGameComponent : GameComponent
    {
        private int _balanceCheckTick = 0;
        private const int BalanceCheckInterval = 15000; // 5 minutes at 60fps ≈ 18000, but using 15000 (~4 min) for responsiveness

        public ModCompatGameComponent(Game game) { }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            Core.ApiLogMonitor.Reset();
            Core.SpamDetector.AutoDetectEnabled = ModCompatMod.Instance.Settings.AutoSpamDetect;            Core.ApiBalanceChecker.Reset();
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            Core.SpamDetector.Tick();

            // Periodic balance check
            var settings = ModCompatMod.Instance?.Settings;
            if (settings != null && settings.EnableBalanceCheck && settings.IsAIConfigured())
            {
                _balanceCheckTick++;
                if (_balanceCheckTick >= BalanceCheckInterval)
                {
                    _balanceCheckTick = 0;
                    Core.ApiBalanceChecker.CheckBalance(settings.APIEndpoint, settings.APIKey);
                }

                // Check for low balance warning
                if (Core.ApiBalanceChecker.LastBalance >= 0 &&
                    Core.ApiBalanceChecker.LastBalance <= settings.BalanceWarningThreshold &&
                    !Core.ApiBalanceChecker.WarningSent &&
                    Core.ApiBalanceChecker.LastCheckTime > System.DateTime.MinValue)
                {
                    Core.ApiBalanceChecker.WarningSent = true;
                    string warningText = "ModCompatChecker.BalanceWarningLetter".Translate() +
                        Core.ApiBalanceChecker.LastBalance.ToString("F2") + " " + Core.ApiBalanceChecker.LastCurrency;
                    Find.LetterStack.ReceiveLetter(
                        "ModCompatChecker.BalanceWarningTitle".Translate(),
                        warningText,
                        LetterDefOf.NegativeEvent,
                        null, 0, true);
                }
            }
        }
    }
}
