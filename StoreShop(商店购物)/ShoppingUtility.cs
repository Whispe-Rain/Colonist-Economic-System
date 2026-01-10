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
            
            
            // 🔑 获取经济组件（唯一正确方式）
            var economy = Current.Game.GetComponent<CES_EconomyGameComponent>();
            ThingFilter tradeFilter = economy?.tradeThingFilter;

            if (tradeFilter == null)
                return new List<Thing>();

            //得到购物区
            var buyArea = map.areaManager.AllAreas.OfType<Area_Buy>().FirstOrDefault();
            if (buyArea == null)
            {
                Log.Message("未找到的购物区");
                return new List<Thing>();
            }
            HashSet<Thing> allItems = new HashSet<Thing>();

            foreach (IntVec3 cell in buyArea.ActiveCells)
            {
                if (!cell.InBounds(map))
                    continue;

                // === 1️⃣ 地面 & 建筑扫描 ===
                var thingsAtCell = map.thingGrid.ThingsListAt(cell);

                foreach (var thing in thingsAtCell)
                {
                    if (thing is Pawn || thing is Corpse || thing is MinifiedThing)
                        continue;

                    // ❌ 不把建筑本体当商品
                    if (thing is Building)
                    {
                        // === 2️⃣ 原版存储：SlotGroup ===
                        SlotGroup slotGroup = thing.GetSlotGroup();
                        if (slotGroup != null)
                        {
                            foreach (Thing stored in slotGroup.HeldThings)
                            {
                                allItems.Add(stored);
                            }
                        }

                        // === 3️⃣ MOD 容器：IThingHolder ===
                        IThingHolder holder = thing as IThingHolder;
                        if (holder == null && thing is ThingWithComps twc)
                        {
                            holder = twc.AllComps.OfType<IThingHolder>().FirstOrDefault();
                        }

                        if (holder != null)
                        {
                            List<Thing> heldThings = new List<Thing>();
                            ThingOwnerUtility.GetAllThingsRecursively(holder, heldThings, allowUnreal: true);
                            foreach (var held in heldThings)
                            {
                                allItems.Add(held);
                            }
                        }

                        continue;
                    }

                    // === 普通地面物品 ===
                    allItems.Add(thing);
                }
            }
            
            IEnumerable<Thing> filteredItems = allItems.Where(t =>
            {
                // 0️⃣ 玩家交易筛选器
                if (!tradeFilter.Allows(t.def))
                    return false;

                // 1️⃣ MinifiedThing 防御（你之前踩过雷）
                if (t is MinifiedThing)
                    return false;

                // 2️⃣ 必须是 Item
                if (t.def.category != ThingCategory.Item)
                    return false;

                // 3️⃣ 市场与交易性
                if (t.MarketValue <= 0f ||
                    t.def.tradeability == Tradeability.None)
                    return false;

                // 4️⃣ 状态检查
                if (t.IsForbidden(pawn) || t.IsBurning())
                    return false;

                // 6️⃣ 排除装备中物品
                if (t.ParentHolder is Pawn)
                    return false;

                // 7️⃣ 最低价值门槛
                if (t.MarketValue < 1f)
                    return false;

                // 8️⃣ 可达性（只对 Spawned）
                if (t.Spawned && !pawn.CanReach(t, PathEndMode.ClosestTouch, Danger.Some))
                    return false;
                //兜底
                if (t.def.IsStuff || t.def.building?.isResourceRock == true)
                    return false;

                return true;
            });
            return filteredItems.ToList().InRandomOrder().ToList();
        }
    }
}
