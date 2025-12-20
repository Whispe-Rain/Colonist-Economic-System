using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace EconomicSystem
{
    /// <summary>
    /// 计算建造工作开始工作量的补丁
    /// </summary>
    [HarmonyPatch(typeof(JobDriver), "SetupToils")]
    public static class Patch_Construction_SetupToils
    {
        static void Postfix(JobDriver_ConstructFinishFrame __instance)
        {
            Pawn pawn = __instance.pawn;
            Job job = __instance.job;

            if (pawn == null || pawn.Faction != Faction.OfPlayer)
                return;

            Thing target = job.targetA.Thing;
            if (target is not Frame frame)
                return;

            // 🔹 记录“初始剩余工作量”
            ConstructionWorkSessionTracker.Start(
                pawn,
                frame,
                frame.WorkLeft
            );
        }
    }


}