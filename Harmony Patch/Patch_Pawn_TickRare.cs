using HarmonyLib;
using Verse;
using RimWorld;


namespace EconomicSystem
{
    /// <summary>
    /// 经济压力补丁，根据欠薪等级设置健康面板的经济压力，减少全局工作速度
    /// </summary>
    // 挂钩到 Pawn 的低频 Tick (每 2000 刻运行一次)
    [HarmonyPatch(typeof(Pawn), "TickRare")]
    public static class Patch_Pawn_TickRare
    {
        
        private const float MaxSeverity = 1.0f;
        
        // 用于防止重复获取 Def
        private static HediffDef EconomicStressDef = null; 

        public static void Postfix(Pawn __instance)
        {
            // 确保只对殖民者进行检查
            if (__instance == null || !__instance.IsColonist || __instance.Dead || __instance.Faction != Faction.OfPlayer)
                return;

            if (EconomicStressDef == null)
            {
                EconomicStressDef = DefDatabase<HediffDef>.GetNamedSilentFail("CES_EconomicStress");
                if (EconomicStressDef == null) return;
            }

            var data = __instance.TryGetEconomyData();
            if (data == null) return;

            float unpaid = data.unpaidWage;
            float newSeverity = 0f;

            // 1. 根据欠薪金额设置严重程度
            if (unpaid > data.GetUnpaidWageLevel(2))
            {
                // 严重压力 (对应 Stage 2)
                // 设置一个高 Severity 确保进入重度阶段
                newSeverity = 0.7f; 
            }
            else if (unpaid > data.GetUnpaidWageLevel(1))
            {
                // 中度压力 (对应 Stage 1)
                newSeverity = 0.4f;
            }
            else if (unpaid > 0)
            {
                // 轻度压力 (对应 Stage 0)
                newSeverity = 0.1f;
            }

            // 2. 应用/更新 Hediff
            Hediff existingHediff = __instance.health.hediffSet.GetFirstHediffOfDef(EconomicStressDef);

            if (newSeverity > 0f)
            {
                // 如果需要添加/更新 Hediff
                if (existingHediff == null)
                {
                    // 还没有 Hediff，添加一个新的
                    Hediff hediff = HediffMaker.MakeHediff(EconomicStressDef, __instance);
                    hediff.Severity = newSeverity;
                    __instance.health.AddHediff(hediff);
                }
                else
                {
                    // 已经有 Hediff，更新 Severity
                    // 如果 Severity 需要变化，才进行设置
                    if (existingHediff.Severity != newSeverity)
                    {
                        existingHediff.Severity = newSeverity;
                    }
                }
            }
            else
            {
                // 欠薪 <= 0，移除 Hediff
                if (existingHediff != null)
                {
                    __instance.health.RemoveHediff(existingHediff);
                    // Log.Message($"[CES Hediff] {__instance.Name.ToStringShort} 欠薪解决，移除了 Hediff。");
                }
            }
        }
    }
}