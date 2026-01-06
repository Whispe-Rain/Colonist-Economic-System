using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace EconomicSystem
{
    public class ForeignUtility
    {
        private static List<ThingDef> cachedInjectableDefs;
        
        
        // 辅助方法 
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
        
        
    
        /// <summary>
        /// 得到等价白银的所有随机商品
        /// </summary>
        /// <param name="maxValue">最大价值</param>
        /// <returns>包含随机物品的列表</returns>
        public static List<ThingDef> GetRandomInThingsDef(int maxValue)
        {
            // 首次调用时缓存所有合适的 ThingDef
            if (cachedInjectableDefs == null)
            {
                cachedInjectableDefs = DefDatabase<ThingDef>.AllDefs
                    // 必须在殖民地环境中有意义的物品
                    .Where(def => def.IsStuff || def.IsIngestible || def.isUnfinishedThing || def.IsApparel || def.IsWeapon) 
                    // 排除一些特殊或不合适的物品
                    .Where(def => !def.IsCorpse && !def.IsShell && def.category == ThingCategory.Item && def.BaseMarketValue > 0f)
                    .Where(def => !def.defName.Contains("Silver")) // 排除白银本身
                    .Where(def => def.FirstThingCategory!=ThingCategoryDefOf.Manufactured)
                    .ToList();
            }
        
            // 筛选出 MarketValue 不超过目标价值的物品，以防止生成价值过高的单品
            var viableDefs = cachedInjectableDefs.Where(def => def.BaseMarketValue <= maxValue).ToList();
        
            if (viableDefs.Any())
            {
                return viableDefs;
            }
        
            // 如果找不到合适的，返回 null
            return null; 
        }
        
    }
}