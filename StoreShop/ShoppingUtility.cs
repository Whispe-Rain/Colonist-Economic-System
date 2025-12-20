using System;
using Verse;
using RimWorld;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using RimWorld.Planet;
using Verse.AI;

namespace EconomicSystem
{
    public static class ShoppingUtility
    {
        public static List<Thing> FindBuyableItemsInStockpiles(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null)
                return new List<Thing>();

            // 1. 获取所有物品：我们应该查找所有 Zone_Stockpile 和 IThingHolder 中的物品
            IEnumerable<Thing> allItems = pawn.Map.zoneManager.AllZones
                .OfType<Zone_Stockpile>() // 查找所有存储区 Zone
                .SelectMany(stockpile => stockpile.AllContainedThings);

            // 2. 添加所有静态存储建筑中的物品 (如货架、衣柜等 IThingHolder)
            allItems = allItems.Concat(pawn.Map.listerThings.AllThings.Where(t => t.ParentHolder is Building_Storage)
                .SelectMany(t => ((IThingHolder)t).GetDirectlyHeldThings()));
            
            // 3. 将待搬运列表中的物品也包括进来，以防有散落在地上的物品
            allItems = allItems.Concat(pawn.Map.listerHaulables.ThingsPotentiallyNeedingHauling());


            // 4. 最终过滤和安全检查
            allItems = allItems.Where(t => 
            {
                // =======================================================
                // 【新增检查：排除所有被打包的物品 (MinifiedThing)】
                if (t is MinifiedThing) 
                    return false;
                // =======================================================

                // 1. 基础检查
                if (t.MarketValue <= 0f || t.IsForbidden(pawn) || t.IsBurning() || t.def.tradeability == Tradeability.None)
                    return false;
        
                // 2. 战略资源和安全库存检查 (调用我们定义的 IsPurchaseAllowed)
                if (!CES_EconomyUtility.IsPurchaseAllowed(t.def, pawn.Map, 1)) 
                    return false; 
        
                // 3. 排除殖民者已装备/穿戴的物品
                if (t is Pawn || t.ParentHolder is Pawn) 
                    return false;
        
                // 4. 可达性检查
                if (!pawn.CanReach(t, PathEndMode.ClosestTouch, Danger.Some))
                    return false;
        
                return true;
            });
    
            // 随机打乱物品列表
            return allItems.Distinct().ToList().InRandomOrder().ToList();
        }
    }
}