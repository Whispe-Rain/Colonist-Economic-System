using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace EconomicSystem
{
    /// <summary>
    /// 计算采矿工作结束工作量的补丁
    /// </summary>
    [HarmonyPatch(typeof(JobDriver), nameof(JobDriver.Cleanup))]
    public static class Patch_Mining_Cleanup
    {
        static void Postfix(JobDriver __instance, JobCondition condition)
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

            float workDone = MiningWorkSessionTracker.End(pawn, mineable);

            if (workDone <= 0f)
                return;
            
            var data = pawn.GetEconomyData();
            if (data == null)
                return;
            
            // ⭐ 得到该工作的工作类型
            WorkGiverDef giverDef = job.workGiverDef;
            WorkTypeDef workType = giverDef.workType;
            
            //从工作字典中得到该工作类型，如果不存在就开一个新的并记录
            if (!data.workValueByType.ContainsKey(workType))
            {
                data.workValueByType[workType] = 0f;
            }
            data.workValueByType[workType] += workDone;
            
            Log.Message(
                $"[CES] 工作完成 | Pawn={pawn.NameShortColored} | " +
                $"工作类型={workType.defName} | " +
                $"工作量={data.workValueByType[workType]}"
            );
        }
    }

}