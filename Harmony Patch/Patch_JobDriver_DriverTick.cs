/*using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace EconomicSystem
{
    [HarmonyPatch(typeof(JobDriver), nameof(JobDriver.DriverTick))]
    public static class Patch_JobDriver_DriverTick
    {
        static void Postfix(JobDriver __instance)
        {
            Pawn pawn = __instance.pawn;
            Job job = __instance.job;

            if (pawn == null || pawn.Faction != Faction.OfPlayer)
                return;

            if (job?.workGiverDef == null)
                return;

            Toil toil = __instance.CurToil; // ⚠️ 受保护 → 我们下面解决

            WorkLaborModel model = WorkLaborModel.None;

            if (toil != null)
                model = WorkValueCalculator.GetLaborModel(toil.defaultCompleteMode);

            if (model == WorkLaborModel.None)
                return;

            if (!WorkValueCalculator.CanRecordThisTick(pawn))
                return;

            WorkValueCalculator.NotifyWorkTick(pawn, job, model);
        }
    }



}*/