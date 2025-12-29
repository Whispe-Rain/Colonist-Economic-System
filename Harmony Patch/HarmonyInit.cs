using HarmonyLib;
using RimWorld;
using Verse;

namespace EconomicSystem
{
    [StaticConstructorOnStartup]
    public static class HarmonyInit
    {
        static HarmonyInit()
        {
            var harmony = new Harmony("Polaris.CES");
            harmony.PatchAll();
            
        }
    }
}