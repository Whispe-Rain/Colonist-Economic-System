using HarmonyLib;
using RimWorld;
using Verse;

namespace EconomicSystem
{
    public static class GuestTrackerReflection
    {
        public static readonly AccessTools.FieldRef<Pawn_GuestTracker, Pawn>
            PawnRef = AccessTools.FieldRefAccess<Pawn_GuestTracker, Pawn>("pawn");
    }
    
    [HarmonyPatch(typeof(Pawn_GuestTracker), nameof(Pawn_GuestTracker.SetGuestStatus))]
    public static class Patch_Pawn_GuestTracker_Economy
    {
        public static void Postfix(Pawn_GuestTracker __instance)
        {
            Pawn pawn = GuestTrackerReflection.PawnRef(__instance);
            if (pawn == null) return;

            var comp = Current.Game.GetComponent<CES_EconomyGameComponent>();
            if (comp == null) return;

            bool shouldHave = comp.ShouldHaveEconomyData(pawn);
            bool has = comp.HasEconomyData(pawn);

            if (shouldHave && !has)
                comp.CreateOrEnableEconomyDataFor(pawn);
            else if (!shouldHave && has)
            {
                comp.RemoveEconomyDataFor(pawn);
                Log.Warning($"{pawn.LabelShort}成为非殖民者，已禁用经济组件");
            }
                
        }
    }


}