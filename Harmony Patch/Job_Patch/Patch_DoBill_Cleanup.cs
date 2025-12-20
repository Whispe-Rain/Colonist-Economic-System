using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace EconomicSystem
{
    // 专门处理 Bill 类 Job(所有有工作量的工作)
    [HarmonyPatch(typeof(JobDriver), nameof(JobDriver.Cleanup))]
    public static class Patch_DoBill_Cleanup
    {
        static void Postfix(JobDriver_DoBill __instance) // 注意类型是 JobDriver_DoBill
        {
            Pawn pawn = __instance.pawn;
            Job job = __instance.job;
            float workLeft=__instance.workLeft;

            // 检查：只处理殖民者和 Bill 类 Job
            if (pawn == null || pawn.Faction != Faction.OfPlayer || job?.bill == null)
                return;

            // --- 【关键逻辑】 ---
            float currentWorkLeft = 0f;
            if (workLeft != null)
            {
                currentWorkLeft = workLeft;
            }
            
            // 2. 调用 Tracker.End 方法
            float actualWorkDone = BillWorkSessionTracker.End(pawn, job, currentWorkLeft);
            
            
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
            data.workValueByType[workType] += actualWorkDone;
            
            
            Log.Message(
                $"[CES] 工作完成 | Pawn={pawn.NameShortColored} | " +
                $"工作类型={workType.defName} | " +
                $"工作量={data.workValueByType[workType]}"
            );
        }
    }
}