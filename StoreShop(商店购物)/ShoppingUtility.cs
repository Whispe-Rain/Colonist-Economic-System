using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace EconomicSystem
{
    public static class ShoppingUtility
    {
        /// <summary>
        /// 查找指定Pawn所在地图购物区域内的所有可购买物品（包括区域内储存箱的递归物品）
        /// 只遍历购物区域格子，避免全地图扫描。
        /// </summary>
        /// <param name="pawn">买家</param>
        /// <returns>符合条件的物品列表（随机排序）</returns>
        public static List<Thing> FindBuyableItemsInStockpiles(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null)
                return new List<Thing>();

            Map map = pawn.Map;

            // 获取购物区域（假设叫"购物区"）
            var buyArea = map.areaManager.GetLabeled("购物区") as Area_Buy;
            if (buyArea == null)
            {
                // 购物区不存在，返回空
                Log.Message("未设置购物区");
                return new List<Thing>();
            }
            
            HashSet<Thing> allItems = new HashSet<Thing>();

            // 1. 遍历购物区域内所有激活格子
            foreach (IntVec3 cell in buyArea.ActiveCells)
            {
                if (!cell.InBounds(map))
                    continue;

                // 1.1 取格子上的所有物品（地图上散落的）
                List<Thing> thingsAtCell = map.thingGrid.ThingsListAt(cell);

                foreach (var thing in thingsAtCell)
                {
                    if (thing.Faction == Faction.OfPlayer && !(thing is Pawn) && !(thing is Corpse) && !(thing is MinifiedThing))
                    {
                        allItems.Add(thing);
                    }
                }
            }

            // 2. 遍历购物区域内所有物体，找储存容器里的物品
            // 包括MOD添加的储物箱（IThingHolder接口）
            foreach (IntVec3 cell in buyArea.ActiveCells)
            {
                if (!cell.InBounds(map))
                    continue;

                var thingsAtCell = map.thingGrid.ThingsListAt(cell);

                foreach (var thing in thingsAtCell)
                {
                    // 排除非玩家势力的和特殊类型
                    if (thing.Faction != Faction.OfPlayer || thing is Pawn || thing is Corpse || thing is MinifiedThing)
                        continue;

                    IThingHolder holder = thing as IThingHolder;

                    // 若物体自身不是容器，尝试查找实现IThingHolder的Comp
                    if (holder == null && thing is ThingWithComps twc)
                    {
                        holder = twc.AllComps.OfType<IThingHolder>().FirstOrDefault();
                    }

                    if (holder != null)
                    {
                        // 递归获取该容器内所有物品
                        List<Thing> heldThings = new List<Thing>();
                        ThingOwnerUtility.GetAllThingsRecursively(holder, heldThings, allowUnreal: true);

                        foreach (var heldThing in heldThings)
                        {
                            allItems.Add(heldThing);
                        }
                    }
                }
            }

            // 3. 过滤满足条件的物品
            IEnumerable<Thing> filteredItems = allItems.Where(t =>
            {
                
                // 先排除建筑物,避免把货架搬走
                if (t is Building)
                    return false;
                
                // 基本检查
                if (t.MarketValue <= 0f || t.IsForbidden(pawn) || t.IsBurning() || t.def.tradeability == Tradeability.None)
                    return false;

                // 交易许可检查
                if (!CES_EconomyUtility.IsPurchaseAllowed(t.def, map, 1))
                    return false;

                // 排除被装备的物品
                if (t.ParentHolder is Pawn)
                    return false;

                // 路径可达性检测
                if (!pawn.CanReach(ThingOwnerUtility.GetFirstSpawnedParentThing(t) ?? t, PathEndMode.ClosestTouch, Danger.Some))
                    return false;

                return true;
            });

            // 返回随机打乱的列表
            return filteredItems.ToList().InRandomOrder().ToList();
        }
    }
}
