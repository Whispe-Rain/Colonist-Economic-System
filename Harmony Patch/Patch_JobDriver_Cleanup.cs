/*using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace EconomicSystem
{
    /// <summary>
    /// 在 Job 真正结束时统计工作
    /// RimWorld 1.6：WorkGiverDef 已经被写入 Job
    /// </summary>
    [HarmonyPatch(typeof(JobDriver), nameof(JobDriver.Cleanup))]
    public static class Patch_JobDriver_Cleanup
    {
        public static void Postfix(JobDriver __instance, JobCondition condition)
        {
            // 只统计成功完成的 Job
            if (condition != JobCondition.Succeeded)
                return;

            Pawn pawn = __instance.pawn;
            if (pawn == null || !pawn.IsColonist)
                return;

            Job job = __instance.job;
            if (job == null)
                return;

            // ⭐ 关键：1.6 中 Job 持有 WorkGiverDef
            WorkGiverDef giverDef = job. workGiverDef;
            if (giverDef == null)
                return; // 非工作类 Job（吃饭、睡觉、社交等）

            WorkTypeDef workType = giverDef.workType;
            if (workType == null)
                return;

            var data = pawn.GetEconomyData();
            if (data == null)
                return;

            // MVP 阶段：一次 Job = 1 点工作价值
            data.totalWorkValue += 1f;

            if (!data.workValueByType.ContainsKey(workType))
                data.workValueByType[workType] = 0f;

            data.workValueByType[workType] += 1f;

            Log.Message(
                $"[CES] 工作完成 | Pawn={pawn.Name} | " +
                $"WorkType={workType.defName} | Job={job.def.defName}"
            );

        }
    }
}*/


using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;


namespace EconomicSystem
{
    // 目标方法：JobDriver 的 JobDriver.Cleanup
    [HarmonyPatch(typeof(JobDriver), nameof(JobDriver.Cleanup))]
    public static class Patch_JobDriver_Cleanup
    {
        // 预先反射获取 JobDriver_DoBill 的私有字段 workLeft
        private static readonly FieldInfo WorkLeftField = AccessTools.Field(typeof(JobDriver_DoBill), "workLeft");
        
        // 只需要 Postfix
        static void Postfix(JobDriver_DoBill __instance) // 注意类型是 JobDriver_DoBill
        {
            Pawn pawn = __instance.pawn;
            Job job = __instance.job;

            // 检查：只处理殖民者和 Bill 类 Job
            if (pawn == null || pawn.Faction != Faction.OfPlayer || job?.bill == null)
                return;

            // --- 【关键逻辑】 ---
            
            // 1. 反射获取当前的剩余工作量 (Job 被中断时就是剩余量，Job 完成时就是 0)
            float currentWorkLeft = 0f;
            if (WorkLeftField != null)
            {
                currentWorkLeft = (float)WorkLeftField.GetValue(__instance);
            }
            
            // 2. 调用 Tracker.End 方法
            float actualWorkDone = BillWorkSessionTracker.End(pawn, job, currentWorkLeft);

            // TODO: 使用 actualWorkDone 进行你的经济系统逻辑
            Log.Message($"[EconomicSystem] CleanupToils: Job ended. Done: {actualWorkDone}");
        }
    }
}
