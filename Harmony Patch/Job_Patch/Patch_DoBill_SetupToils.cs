using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace EconomicSystem
{
    /// <summary>
    /// 处理有Bill的工作开始时的工作量(锻造，缝纫，制作)
    /// 在 Job 开始执行时记录 Bill 的初始 workLeft
    /// </summary>
    // 修改点：直接使用字符串 "SetupToils"
    [HarmonyPatch(typeof(JobDriver), "SetupToils")] 
    public static class Patch_DoBill_SetupToils
    {
        static void Postfix(JobDriver __instance)
        {
            Pawn pawn = __instance.pawn;
            Job job = __instance.job;

            // 只处理殖民者
            if (pawn == null || pawn.Faction != Faction.OfPlayer)
                return;
            // 只处理 Bill 类 Job (比如切石、烹饪、锻造等)
            if (job?.bill == null)
                return;
            
            float initialWorkLeft;
            RecipeDef recipe = job.bill.recipe;
            UnfinishedThing unfinishedThing = null;
            
            // 1. 检查 Bill 是否是带有半成品绑定的类型
            if (job.bill is Bill_ProductionWithUft productionBill)
            {
                // 2. 尝试获取 Bill 绑定的半成品 (BoundUft)
                unfinishedThing = productionBill.BoundUft;
            }

            if (unfinishedThing != null)
            {
                // **分支一：处理半成品**
                initialWorkLeft = unfinishedThing.workLeft;
                Log.Message($"通过 BoundBill 找到半成品: 工作量:{initialWorkLeft / 60f} (剩余Ticks: {initialWorkLeft})");
            }
            else
            {
                // **分支二：全新工作 或 JobDriver 未将半成品绑定到 Job 目标**
                
                // 此时执行正常的全新工作计算逻辑，同时加上额外的防御性检查
                
                ThingDef stuffDefToUse = null;
                Thing targetThingA = job.GetTarget(TargetIndex.A).Thing;
                
                if (recipe.products.Any(p => p.thingDef.MadeFromStuff) && targetThingA != null)
                {
                    stuffDefToUse = targetThingA.Stuff; 
                }
                
                initialWorkLeft = recipe.WorkAmountForStuff(stuffDefToUse);
                Log.Message($"开始全新工作或未绑定半成品Job: 总工作量:{initialWorkLeft } (总Ticks: {initialWorkLeft})"); 
            }
            // 你的业务逻辑
            BillWorkSessionTracker.Begin(pawn, job,initialWorkLeft);
        }
    }
}