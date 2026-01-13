using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace EconomicSystem
{
    public class ForeignUtility
    {
        private static List<ThingDef> cachedInjectableDefs;
        
        
        // 辅助方法 
        public static bool IsViableTrader(Pawn forPawn, Pawn p)
        {
            const float MaxTradeDistance = 40f;

            if (!p.Spawned || p.Downed)
                return false;

            if (p.Faction == null || p.Faction == Faction.OfPlayer)
                return false;

            if (p.HostileTo(forPawn))
                return false;

            if (p.IsPrisoner || p.IsFormingCaravan())
                return false;

            // ⭐ 核心区分点：必须是真正的商队
            Lord lord = p.GetLord();
            if (lord?.LordJob is not LordJob_TradeWithColony)
                return false;

            // TraderKind 作为“补充条件”，而不是核心条件
            if (p.TraderKind == null)
                return false;

            return forPawn.Position.InHorDistOf(p.Position, MaxTradeDistance);
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
        
        /// <summary>
        /// 选择随机材质
        /// </summary>
        /// <param name="def">物品def</param>
        /// <returns></returns>
        public static ThingDef ChooseSafeRandomStuff(ThingDef def)
        {
            //该物品不需要材质
            if (!def.MadeFromStuff)
                return null;

            //得到所有的材质
            var allowed = GenStuff.AllowedStuffsFor(def);
            if (allowed == null || !allowed.Any())
            {
                Log.Warning($"[CES] No allowed stuff for {def.defName}");
                return null;
            }
            
            //只生成符合该物品科技等级的材质
            allowed = allowed
                .Where(s => s.techLevel <=def.techLevel)
                .ToList();

            // 权重随机：便宜的更常见
            return allowed.RandomElementByWeight(stuff =>
            {
                // 越贵，权重越低
                return 1f / Mathf.Sqrt(stuff.BaseMarketValue + 1f);
            });
        }
        

        
    }
}