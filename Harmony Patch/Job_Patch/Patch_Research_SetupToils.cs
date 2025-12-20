using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace EconomicSystem
{
    [HarmonyPatch(typeof(JobDriver), "SetupToils")]
    public static class Patch_Research_SetupToils
    {
        static void Postfix(JobDriver __instance)
        {
            if (__instance.job?.def != JobDefOf.Research)
                return;

            Pawn pawn = __instance.pawn;
            if (pawn == null || pawn.Faction != Faction.OfPlayer)
                return;

            ResearchWorkSessionTracker.Begin(pawn);

            Log.Message(
                $"[CES][Research] Begin {pawn.LabelShort}"
            );
        }
    }

}