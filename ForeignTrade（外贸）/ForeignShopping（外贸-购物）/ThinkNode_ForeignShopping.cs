using RimWorld;
using Verse;
using Verse.AI;

namespace EconomicSystem
{
    public class ThinkNode_ForeignShopping:ThinkNode_Conditional
    {
        protected override bool Satisfied(Pawn pawn)
        {
            // ===== 2.物品条件 =====
            var data = pawn.GetEconomyData();
            if (data == null) {  return false; }
            
            //首先检测外贸购物CD
            int now = Find.TickManager.TicksGame;
            if (now < data.ForeignShoppingTick)
                return false;
            
            // ===== 1. 基础安全检查 =====
            if (pawn == null || pawn.Map == null) {  return false; }

            if (!pawn.IsColonist) { return false; }

            if (pawn.Dead || pawn.Downed) {  return false; }

            if (pawn.InMentalState) {  return false; }

            if (pawn.Drafted || pawn.jobs?.curJob?.playerForced == true) { return false; }
    
            
            
            //资产至少要有500
            if (data.virtualWallet<500)
            {
                return false;
            }
            
            
            
            //浮动购买欲望(有钱人，心情好的人更愿意消费)
            float chance = 0.1f;

            if (data.virtualWallet > 1500) chance += 0.05f;
            if (pawn.needs?.mood?.CurLevel > 0.6f) chance += 0.05f;

            if (!Rand.Chance(chance))
                return false;
            
            //====3.(核心)商队检查====
            bool caravanPresent = IsTradeCaravanPresent(pawn.Map);
    
            return caravanPresent;
        }
        private bool IsTradeCaravanPresent(Map map)
        {
            // 遍历地图上的所有Pawn
            foreach (Pawn currentPawn in map.mapPawns.AllPawnsSpawned)
            {
                // 1. 排除玩家派系Pawn、中立/无派系Pawn（如野生动物）
                if (currentPawn.Faction == Faction.OfPlayer || currentPawn.Faction == null)
                {
                    continue;
                }

                // 2. 排除敌对Pawn
                if (currentPawn.Faction.HostileTo(Faction.OfPlayer))
                {
                    continue;
                }
                
                // 3. 核心检查：Pawn 是否具有 TraderKind（贸易代表）
                if (currentPawn.TraderKind != null)
                {
                    return true;
                }
            }
            return false;
        }
    }
}