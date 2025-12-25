using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace EconomicSystem
{
    /// <summary>
    /// 计算建造工作结束工作量的补丁
    /// </summary>
    [HarmonyPatch(typeof(JobDriver), nameof(JobDriver.Cleanup))]
    public static class Patch_Construction_Cleanup
    {
        static void Postfix(JobDriver_ConstructFinishFrame __instance, JobCondition condition)
        {
            Pawn pawn = __instance.pawn;
            Job job = __instance.job;
            
            

            if (pawn == null || pawn.Faction != Faction.OfPlayer)
                return;

            Thing target = job.targetA.Thing;
            if (target == null)
                return;

            float workLeft = 0f;

            if (target is Frame frame)
                workLeft = frame.WorkLeft;
            else
                return;

            float workDone =
                ConstructionWorkSessionTracker.End(pawn, target, workLeft);

            if (workDone <= 0f)
                return;
            
            var data = pawn.GetEconomyData();
            if (data == null)
                return;
            
            // ⭐ 得到该工作的工作类型
            WorkGiverDef giverDef = job.workGiverDef;
            WorkTypeDef workType = giverDef.workType;
            
            if (!data.workValueByType.ContainsKey(workType))
            {
                data.workValueByType[workType] = 0f;
            }
            data.workValueByType[workType] += workDone/60;
            
            /*Log.Message(
                $"[CES] 工作完成 | Pawn={pawn.NameShortColored} | " +
                $"工作类型={workType.defName} | " +
                $"工作量={data.workValueByType[workType]}"
            );*/
        }
    }
}