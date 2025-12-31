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

            //得到购物区
            var buyArea = map.areaManager.AllAreas.OfType<Area_Buy>().FirstOrDefault();
            if (buyArea == null)
            {
                Log.Message("未找到 Area_Buy 类型的购物区");
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

            Log.Warning("初步筛选："+allItems.Count);
            // 3. 过滤满足条件的物品
            IEnumerable<Thing> filteredItems = allItems.Where(t =>
            {
                // === 致命防御：MinifiedThing ===
                if (t is MinifiedThing minified)
                {
                    // inner 为空 → 直接丢弃
                    if (minified.InnerThing == null)
                        return false;

                    // 即使 inner 不为空，也不允许交易
                    return false;
                }

                
                // 例如：必须是 Item
                if (t.def.category != ThingCategory.Item)
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
                
                // ✅ 只有【已 Spawn 的地面物品】才做 CanReach
                if (t.Spawned)
                {
                    if (!pawn.CanReach(t, PathEndMode.ClosestTouch, Danger.Some))
                        return false;
                }
                
                return true;
            });
            Log.Warning("最终："+filteredItems.ToList().Count);
            // 返回随机打乱的列表
            return filteredItems.ToList().InRandomOrder().ToList();
        }
    }
}
