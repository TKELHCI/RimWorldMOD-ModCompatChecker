using UnityEngine;
using Verse;

namespace ModCompatChecker
{
    [StaticConstructorOnStartup]
    public class ModCompatGameComponent : GameComponent
    {
        public ModCompatGameComponent(Game game) { }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            Core.SpamDetector.Tick();
        }
    }
}