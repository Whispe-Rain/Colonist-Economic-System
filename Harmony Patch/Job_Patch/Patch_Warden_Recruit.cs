using HarmonyLib;
using RimWorld;
using Verse;
using System.Threading;

namespace EconomicSystem
{
    

    /// <summary>
    /// 招募工作：降低抵抗值统计（基于 Resistance 的减少）
    /// Patch 目标：InteractionWorker_RecruitAttempt.Interacted
    /// </summary>
    [HarmonyPatch(typeof(InteractionWorker_RecruitAttempt), nameof(InteractionWorker_RecruitAttempt.Interacted))]
    public static class Patch_Warden_Recruit
    {
        private static float initialResistance;
        // --- Prefix: 记录减少前的 Resistance 值 ---
        static void Prefix(Pawn recipient)
        {
            // 只有当囚犯是人型且有抵抗值时，才记录
            if (recipient?.guest != null && recipient.RaceProps.Humanlike && recipient.guest.resistance > 0f)
            {
                initialResistance = recipient.guest.resistance;
            }
            else
            {
                initialResistance = 0f; 
            }
        }

        // --- Postfix: 计算差值并记录工作价值 ---
        static void Postfix(Pawn initiator, Pawn recipient)
        {
            // 此方法不依赖 Ideology DLC，但最好保留检查以防万一
            // if (!ModsConfig.IdeologyActive) return; // (注: 招募是基础游戏功能，无需 DLC 检查)
            
            Pawn warden = initiator; // 工作者 (招募者)
            Pawn prisoner = recipient; // 被工作者 (囚犯)

            // 1. 基本检查
            if (warden == null || !warden.IsColonist || prisoner == null || !prisoner.RaceProps.Humanlike)
                return;

            // 2. 确保被工作者不属于玩家派系
            if (prisoner.Faction == Faction.OfPlayer)
                return;

            // 3. 计算实际减少量
            float resistanceReduced = 0f;
            if (prisoner.guest != null && initialResistance > 0f)
            {
                // 减少量 = 减少前的值 - 减少后的值
                resistanceReduced = initialResistance - prisoner.guest.resistance;
            }
            
            // 4. 确保有有效的 Resistance 减少量（考虑浮点数误差）
            if (resistanceReduced <= 0.001f) 
                return;

            // 5. 记录工作量
            var data = warden.GetEconomyData();
            if (data == null)
                return;

            // 抵抗值减少量作为工作价值
            data.AddWork(WorkTypeDefOf.Warden, resistanceReduced*5); 

            Log.Message(
                $"[CES] 招募-降抵抗 | {warden.NameShortColored} | " +
                $"目标={prisoner.LabelShortCap} | 工作量={resistanceReduced:F2}"
            );
        }
    }
}