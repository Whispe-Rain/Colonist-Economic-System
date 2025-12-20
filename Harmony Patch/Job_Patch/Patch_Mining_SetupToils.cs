using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace EconomicSystem
{
    /// <summary>
    /// 计算采矿工作开始工作量的补丁
    /// </summary>
    [HarmonyPatch(typeof(JobDriver), "SetupToils")]
    public static class Patch_Mining_SetupToils
    {
        static void Postfix(JobDriver __instance)
        {
            if (__instance is not JobDriver_Mine mineDriver)
                return;

            Pawn pawn = mineDriver.pawn;
            Job job = mineDriver.job;

            if (pawn == null || pawn.Faction != Faction.OfPlayer)
                return;

            Thing target = job?.targetA.Thing;
            if (target is not Mineable mineable)
                return;

            MiningWorkSessionTracker.Begin(pawn, mineable);

            Log.Message(
                $"[CES][Mining] Begin {pawn.LabelShort} HP={mineable.HitPoints}"
            );
        }
    }

}