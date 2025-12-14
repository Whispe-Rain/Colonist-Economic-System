using HarmonyLib;
using Verse;

namespace EconomicSystem
{
    [StaticConstructorOnStartup]
    public static class HarmonyInit
    {
        static HarmonyInit()
        {
            var harmony = new Harmony("Astesia.EconomicSystem");
            harmony.PatchAll();
        }
    }
}
