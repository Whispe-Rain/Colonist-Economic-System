using RimWorld;
using Verse;

namespace EconomicSystem
{
    public class MainButtonWorker_Economy : MainButtonWorker
    {
        
        public override void Activate()
        {
            var econ = Current.Game.GetComponent<CES_EconomyGameComponent>();
            if (econ == null)
            {
                Log.Error("[CES] EconomyGameComponent not found");
                return;
            }

            if (econ.tradeThingFilter == null)
            {
                Log.Warning("[CES] tradeThingFilter was null, initializing");

                econ.tradeThingFilter = new ThingFilter();
                econ.tradeThingFilter.SetAllowAll(null);
                econ.tradeThingFilter.ResolveReferences();
            }

            Find.WindowStack.Add(
                new Dialog_TradeThingFilter(econ.tradeThingFilter)
            );
        }
    }
}