

using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;


namespace EconomicSystem
{
    /// <summary>
    /// 处理所有按照次数结算的工作(搬运，清理，烹饪等)
    /// 特点：一旦中断工作，只能从头开始。
    /// </summary>
    // 修改点：直接使用字符串 "SetupToils"
    [HarmonyPatch(typeof(JobDriver), nameof(JobDriver.Cleanup))]
    public static class Patch_Default_Cleanup
    {
        //*特殊工作排除表(单独编写了补丁的工作)，暂不使用
        private static readonly HashSet<WorkTypeDef> ExcludedWorkTypes =
            new HashSet<WorkTypeDef>
            {
                WorkTypeDefOf.Research,
                WorkTypeDefOf.Construction,
                WorkTypeDefOf.Mining,
                WorkTypeDefOf.Hunting,
                WorkTypeDefOf.Doctor,
                WorkTypeDefOf.Warden,
                // 以后加特殊工作，只要往这里塞
            };
        //一次性任务。
        public static void Postfix(JobDriver __instance, JobCondition condition)
        {
            /*// 只统计成功完成的 Job
            if (condition != JobCondition.Succeeded)
                return;*/

            Pawn pawn = __instance.pawn;
            if (pawn == null || !pawn.IsColonist)
                return;

            Job job = __instance.job;
            if (job == null||job?.bill!= null)
                return;
            //工作没有完全完成（被中断）时，忽略
            if (condition != JobCondition.Succeeded)
                return;

            // ⭐ 关键：1.6 中 Job 持有 WorkGiverDef
            WorkGiverDef giverDef = job.workGiverDef;
            if (giverDef == null)
                return; // 非工作类 Job（吃饭、睡觉、社交等）

            WorkTypeDef workType = giverDef.workType;
            
            if (workType == null)
                return;
            
            /*//特殊工作,跳过(会导致一个大类的工作内部的小工作全部无价值)
            if (ExcludedWorkTypes.Contains(workType))
                return;*/

            var data = pawn.GetEconomyData();
            if (data == null)
                return;

            //从工作字典中得到该工作类型，如果不存在就开一个新的并记录
            if (!data.workValueByType.ContainsKey(workType))
            {
                data.workValueByType[workType] = 0f;
            }
            data.workValueByType[workType] += 0.2f;

            Log.Message(
                $"[CES] 工作完成 | Pawn={pawn.NameShortColored} | " +
                $"工作类型={workType.defName} | " +
                $"工作量={data.workValueByType[workType]}"
            );
        }
    }
    
        
}
