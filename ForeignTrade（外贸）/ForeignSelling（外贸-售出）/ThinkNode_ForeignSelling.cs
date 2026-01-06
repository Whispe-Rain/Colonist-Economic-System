using RimWorld;
using Verse;
using Verse.AI;

namespace EconomicSystem
{
    public class ThinkNode_ForeignSelling:ThinkNode_Conditional
    {
        protected override bool Satisfied(Pawn pawn)
        {
            // ===== 1. 基础安全检查 =====
            if (pawn == null || pawn.Map == null) {  return false; }

            if (!pawn.IsColonist) { return false; }

            if (pawn.Dead || pawn.Downed) {  return false; }

            if (pawn.InMentalState) {  return false; }

            if (pawn.Drafted || pawn.jobs?.curJob?.playerForced == true) { return false; }
    
            // ===== 2.物品条件 =====
            var econ = pawn.GetEconomyData();
            if (econ == null) {  return false; }

            // 重点检查此处：殖民者的资产数量是否大于 0
            if (econ.privateOwnedAssets.Count <= 0) { 
                return false; 
            }
    
            //====3.(核心)商队检查====
            bool caravanPresent = IsTradeCaravanPresent(pawn.Map);
    
            return caravanPresent;
        }
        
        /// <summary>
        /// 检查地图上是否存在非敌对的贸易商队。
        /// </summary>
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