using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace EconomicSystem
{
    public class ForeignTradeUtility
    {
        // 辅助方法 1: 将您原来的 FindBestTraderPawn 逻辑的检查部分移到这里
        public static bool IsViableTrader(Pawn forPawn, Pawn p)
        {
            const float MaxTradeDistance = 40f; 

            // ** 1. 核心检查：是否是真正的访客或商队 Pawn **
            // 检查 TraderKind 是否存在，这几乎是识别商队 Pawn 的唯一方法
            if (p.TraderKind == null)
            {
                // 排除掉所有没有 TraderKind 的普通访客 Pawn、囚犯、友方殖民者等。
                return false;
            }
    
            // ** 2. 基础状态和安全检查 **
            return p.Spawned // 必须在地图上
                   && !p.Downed // 不能倒地
                   && !p.HostileTo(forPawn) // 必须非敌对
                   && p.Faction != Faction.OfPlayer // 必须是非玩家派系
                   && p.IsFormingCaravan() == false // 排除正在组建大篷车的 Pawn
                   && p.IsPrisoner == false // 排除囚犯
                   // ** 3. 距离检查 **
                   && forPawn.Position.InHorDistOf(p.Position, MaxTradeDistance);
        }
        
        public static PrivateItemData GetRandomItemToSell(List<PrivateItemData> items)
        {
            if (items.NullOrEmpty())
                return null;

            // 筛选出 MarketValue > 0 的物品（需要 RecreateThing 来验证）
            List<PrivateItemData> sellableItems = items.Where(itemData =>
            {
                Thing recreatedThing = itemData.RecreateThing();
                if (recreatedThing == null) return false;

                bool canSell = recreatedThing.MarketValue > 0f;
                recreatedThing.Destroy(); // 立即销毁，它是虚拟的
                return canSell;
            }).ToList();

            if (sellableItems.NullOrEmpty())
            {
                return null;
            }

            // 从筛选后的列表中随机选择一个
            return sellableItems.RandomElement(); 
        }
    }
}