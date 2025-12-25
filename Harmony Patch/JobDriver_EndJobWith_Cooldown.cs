using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace EconomicSystem.HarmonyPatches
{
    // 假设您的 JobDef 引用
    [DefOf]
    public static class CES_JobDefOf
    {
        public static JobDef CES_GoShopping;
        public static JobDef CES_ForeignTrade;
        public static JobDef CES_GoTrading;
        // 任何需要冷却的 JobDef 都应该放在这里
    }
    
    // 补丁目标：JobDriver.EndJobWith
    [HarmonyPatch(typeof(JobDriver), nameof(JobDriver.EndJobWith))]
    public static class JobDriver_EndJobWith_Cooldown
    {
        // __instance 是当前的 JobDriver 实例
        // condition 是 Job 结束的条件
        public static void Prefix(JobDriver __instance, JobCondition condition)
        {
            // 1. 只有当 Job 失败时才需要设置冷却
            // Incompletable 和 Errored 是导致 Job 循环的主要原因
            if (condition != JobCondition.Incompletable && condition != JobCondition.Errored)
            {
                // Succeeded/Ongoing/Waiting/Queued 等条件不应触发失败冷却
                return;
            }

            Pawn pawn = __instance.pawn;
            JobDef jobDef = __instance.job.def;
            
            // 2. 确定冷却键名和时长
            string cooldownKey = null;
            int durationTicks = 0; // 默认时长
            
            // 3. 检查 Job Def 是否需要冷却
            if (jobDef == CES_JobDefOf.CES_GoShopping)
            {
                cooldownKey = "ShoppingFail";
                durationTicks = 900; // 10秒冷却
            }
            else if (jobDef == CES_JobDefOf.CES_ForeignTrade)
            {
                cooldownKey = "ForeignFail";
                durationTicks = 900; // 30秒冷却 (高风险行为，冷却更长)
            }
            else if (jobDef == CES_JobDefOf.CES_GoTrading)
            {
                cooldownKey = "TransactionFail";
                durationTicks = 900; // 30秒冷却 (高风险行为，冷却更长)
            }
            // 可以继续添加其他需要冷却的 Job
            
            // 4. 如果确定需要冷却，则设置它
            if (cooldownKey != null)
            {
                CES_PawnEconomyData mindState = pawn.GetEconomyData();
                
                if (mindState != null)
                {
                    mindState.SetCooldown(cooldownKey, durationTicks);
                    Log.Message($"[CES_DEBUG] JobCooldown: {pawn.NameShortColored} job {jobDef.defName} failed with {condition}, setting {cooldownKey} cooldown for {durationTicks} ticks.");
                }
            }
        }
    }
}