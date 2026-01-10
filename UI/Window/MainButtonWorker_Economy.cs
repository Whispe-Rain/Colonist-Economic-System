using RimWorld;
using Verse;

namespace EconomicSystem
{
    public class MainButtonWorker_Economy : MainButtonWorker
    {
        
        public override void Activate()
        {
            Find.WindowStack.Add(new Dialog_EconomyRoot());
        }
    }
}