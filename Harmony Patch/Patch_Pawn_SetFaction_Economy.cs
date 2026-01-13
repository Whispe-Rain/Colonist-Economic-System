using HarmonyLib;
using RimWorld;
using Verse;

namespace EconomicSystem
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SetFaction))]
    public class Patch_Pawn_SetFaction_Economy
    {
        public static void Postfix(Pawn __instance)
        {
            var comp = Current.Game.GetComponent<CES_EconomyGameComponent>();
            if (comp == null) return;

            bool shouldHave = comp.ShouldHaveEconomyData(__instance);
            bool has = comp.HasEconomyData(__instance);

            if (shouldHave && !has)
                comp.CreateOrEnableEconomyDataFor(__instance);
            else if (!shouldHave && has)
            {
                comp.RemoveEconomyDataFor(__instance);
                Log.Warning($"{__instance.LabelShort}派系改变，已禁用经济组件");
            }
                
        }
    }
}