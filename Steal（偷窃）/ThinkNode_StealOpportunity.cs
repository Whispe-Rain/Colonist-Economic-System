using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace EconomicSystem
{
    public class ThinkNode_StealOpportunity : ThinkNode_Conditional
    {
       
        protected override bool Satisfied(Pawn pawn)
        {
            // ===== 1. 基础安全检查 =====
            if (pawn == null || pawn.Map == null)
                return false;

            if (!pawn.IsColonist)
                return false;

            if (pawn.Dead || pawn.Downed)
                return false;

            // 精神状态中不允许偷窃（防止和 MentalState 打架）
            if (pawn.InMentalState)
                return false;

            // 玩家正在强制控制（Draft / Forced Job）
            if (pawn.Drafted || pawn.jobs?.curJob?.playerForced == true)
                return false;

            // ===== 2. 钱包条件 =====
            var econ = pawn.GetEconomyData();
            if (econ == null)
                return false;

            if (econ.virtualWallet >= 400)
                return false;

            // ===== 3. 夜晚判断 =====
            int hour = GenLocalDate.HourOfDay(pawn.Map);
            bool isNight = hour >= 22 || hour < 6;
            if (!isNight)
                return false;

            // ===== 4. 所有殖民者是否都在睡觉 =====
            foreach (Pawn other in pawn.Map.mapPawns.FreeColonists)
            {
                //记录当前有多少人没睡觉
                int pawnAwke = 0;
                
                if (other == pawn)
                    continue;
                
                if (other.Awake()||other!=pawn)
                {
                    pawnAwke += 1;
                }
                // 有三个醒着的殖民者（危险） → 不偷
                if (pawnAwke>3)
                    return false;
            }
            return true;
            
           
        }
    }
}