using System;
using HarmonyLib;
using RimWorld;
using Verse;
using System.Threading;

namespace EconomicSystem
{
    /// <summary>
    /// 奴役工作：降低意志力统计（基于 Will 的减少）
    /// Patch 目标：InteractionWorker_EnslaveAttempt.Interacted
    /// </summary>
    [HarmonyPatch(typeof(InteractionWorker_EnslaveAttempt), nameof(InteractionWorker_EnslaveAttempt.Interacted))]
    public static class Patch_Warden_Enslave
    {
        private static float initialWill;
        // --- Prefix: 在 Interacted 方法执行前，记录当前的意志力 ---
        static void Prefix(Pawn recipient)
        {
            // 在方法执行前，检查并记录囚犯当前的 Will 值
            if (recipient?.guest != null && recipient.guest.will > 0f)
            {
                initialWill = recipient.guest.will;
            }
            else
            {
                initialWill = 0f; 
            }
        }

        // --- Postfix: 在 Interacted 方法执行后，计算差值并记录工作价值 ---
        static void Postfix(Pawn initiator, Pawn recipient)
        {
            // 确保 Ideology DLC 已启用，此代码是 DLC 内容
            if (!ModsConfig.IdeologyActive)
                return;
            
            Pawn warden = initiator; // 工作者 (看守)
            Pawn prisoner = recipient; // 被工作者 (囚犯)

            // 1. 基本检查
            if (warden == null || !warden.IsColonist || prisoner == null)
                return;

            // 2. 确保被工作者不属于玩家派系
            if (prisoner.Faction == Faction.OfPlayer)
                return;

            // 3. 计算实际减少量
            float willReduced = 0f;
            if (prisoner.guest != null && initialWill > 0f)
            {
                // 减少量 = 减少前的值 - 减少后的值
                willReduced = initialWill - prisoner.guest.will;
            }
            
            // 4. 确保有有效的 Will 减少量（考虑浮点数误差）
            if (willReduced <= 0.001f) 
                return;

            // 5. 记录工作量
            var data = warden.GetEconomyData();
            if (data == null)
                return;

            // 意志力减少量作为工作价值
            data.AddWork(WorkTypeDefOf.Warden, willReduced*5); 

            Log.Message(
                $"[CES] 奴役-降意志 | {warden.NameShortColored} | " +
                $"目标={prisoner.LabelShortCap} | 工作量={willReduced:F2}"
            );
        }
    }
}