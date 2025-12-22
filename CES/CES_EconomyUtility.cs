// 文件: CES_EconomyUtility.cs (最简化的、鲁棒的白银查找)

using System.Collections.Generic;
using Verse;
using RimWorld;
using System.Linq;

namespace EconomicSystem
{
    public static class CES_EconomyUtility
    {
        /// <summary>
        /// 计算殖民地当前所有白银总量。
        /// </summary>
        public static float GetColonySilverTotal(Map map)
        {
            if (map == null)
            {
                return 0f;
            }

            // 直接通过 listerThings 查找所有白银堆栈并求和。
            // 这是最不容易出错的方法。
            float totalSilver = map.listerThings.ThingsOfDef(ThingDefOf.Silver)
                .Sum(t => t.stackCount);

            return totalSilver;
        }
        
        // --- 战略资源保护列表 ---
        // 殖民者禁止购买的物品定义
        private static readonly HashSet<ThingDef> ProtectedResources = new HashSet<ThingDef>
        {
            ThingDefOf.MedicineHerbal,
            ThingDefOf.MedicineIndustrial,
            ThingDefOf.MedicineUltratech,
            ThingDefOf.ComponentIndustrial,
            ThingDefOf.Gold,
            ThingDefOf.Silver, // 禁止购买白银本身
            ThingDefOf.Uranium,
            ThingDefOf.Steel,
            ThingDefOf.WoodLog,
            ThingDefOf.Plasteel,
            ThingDefOf.RawPotatoes,
            ThingDefOf.Meat_Human,
            ThingDefOf.MealSimple,
            ThingDefOf.MealFine,
            ThingDefOf.MealSurvivalPack,
            
            
            // 可根据需要添加：高级组件 (ComponentIndustrial, ComponentSpacer)
        };
        
        /// <summary>
        /// 检查殖民者是否允许购买给定数量的物品。
        /// </summary>
        /// <param name="itemDef">物品定义。</param>
        /// <param name="map">物品所在地图。</param>
        /// <param name="count">殖民者计划购买的数量。</param>
        /// <returns>如果允许购买，则返回 true。</returns>
        public static bool IsPurchaseAllowed(ThingDef itemDef, Map map, int count)
        {
            // 1. 战略资源黑名单检查
            if (ProtectedResources.Contains(itemDef))
            {
                return false;
            }

            //任意商品不允许一次性购买超过5个
            if (count>5)
            {
                return false;
            }
            return true;
        }
    }
}