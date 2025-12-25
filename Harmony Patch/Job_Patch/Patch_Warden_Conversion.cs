using HarmonyLib;
using RimWorld;
using Verse;

namespace EconomicSystem
{
    /// <summary>
    /// 教化工作：降低文化认同度统计（基于 Certainty 的减少）
    /// Patch 目标：InteractionWorker_ConvertIdeoAttempt.CertaintyReduction (静态方法)
    /// </summary>
    [HarmonyPatch(typeof(InteractionWorker_ConvertIdeoAttempt), nameof(InteractionWorker_ConvertIdeoAttempt.CertaintyReduction))]
    public static class Patch_Warden_Conversion
    {
        // CertaintyReduction 的签名是 public static float CertaintyReduction(Pawn initiator, Pawn recipient)
        
        // --- Postfix: 拦截 CertaintyReduction 的返回值，该值即为工作量 ---
        static void Postfix(Pawn initiator, Pawn recipient, float __result)
        {
            // 确保 Ideology DLC 已启用
            if (!ModsConfig.IdeologyActive)
                return;
            
            Pawn warden = initiator; // 工作者 (教化者)
            Pawn prisoner = recipient; // 被工作者 (囚犯)

            // 1. 基本检查
            if (warden == null || !warden.IsColonist || prisoner == null)
                return;

            // 2. 确保被工作者不属于玩家派系
            if (prisoner.Faction == Faction.OfPlayer)
                return;

            // 3. 获取实际减少量
            // __result 已经是 CertaintyReduction 计算出的减少值
            float certaintyReduced = __result;
            
            // 4. 确保有有效的 Certainty 减少量
            if (certaintyReduced <= 0.001f) 
                return;

            // 5. 记录工作量
            var data = warden.GetEconomyData();
            if (data == null)
                return;

            // 认同度减少量作为工作价值
            data.AddWork(WorkTypeDefOf.Warden, certaintyReduced*100); 

            /*Log.Message(
                $"[CES] 教化-降认同 | {warden.NameShortColored} | " +
                $"目标={prisoner.LabelShortCap} | 工作量={certaintyReduced:F4}" // 使用 F4 更精确显示小数值
            );*/
        }
    }
}