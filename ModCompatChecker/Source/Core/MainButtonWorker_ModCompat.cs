using ModCompatChecker.UI;
using RimWorld;
using Verse;

namespace ModCompatChecker.Core
{
    public class MainButtonWorker_ModCompat : MainButtonWorker
    {
        public override void Activate()
        {
            Find.WindowStack.Add(new UnifiedWindow());
        }
    }
}
