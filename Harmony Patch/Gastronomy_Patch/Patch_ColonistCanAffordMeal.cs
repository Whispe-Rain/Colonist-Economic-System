using System;
using HarmonyLib;
using Verse;
using EconomicSystem;
using Gastronomy.Restaurant;
using RimWorld;
using System.Reflection; // 引入反射命名空间

/// <summary>
/// 检查殖民者的虚拟钱包内的资产能否负担得起饭钱
/// </summary>
namespace EconomicSystem
{
    [HarmonyPatch(typeof(RestaurantStock), "CanAfford")]
    [HarmonyPatch(new Type[] { typeof(Pawn), typeof(ThingDef) })]
    public static class Patch_ColonistCanAffordMeal
    {
        // 使用 Harmony 的 AccessTools 来获取私有属性 Restaurant 的 Getter
        private static readonly MethodInfo RestaurantGetter = 
            AccessTools.PropertyGetter(typeof(RestaurantStock), "Restaurant");

        public static bool Prefix(Pawn pawn, ThingDef def, RestaurantStock __instance, ref bool __result)
        {
            // 仅拦截我方殖民者
            if (pawn.IsColonist && pawn.Faction == Faction.OfPlayer)
            {
                var economyData = pawn.GetEconomyData();
            
                if (economyData != null)
                {
                    // **核心修正：使用反射获取私有属性 Restaurant**
                    RestaurantController restaurant = (RestaurantController)RestaurantGetter.Invoke(__instance, null);

                    if (restaurant == null)
                    {
                        // 如果反射失败或 Restaurant 为空，则回退到原版逻辑
                        return true;
                    }
                
                    // 获取餐厅设定的价格
                    // 注意：这里需要再次修正 economyData.virtualWallet 为 .Amount
                    float price = def.GetPrice(restaurant);
                
                    // 如果虚拟钱包余额 >= 价格
                    if (economyData.virtualWallet>= price)
                    {
                        __result = true; // 强制“能否负担”检查通过
                        return false;    // 跳过原方法 (原方法不再执行)
                    }
                }
            }
        
            // 其他情况，执行原方法逻辑 (例如：访客或余额不足的殖民者)
            return true; 
        }
    }
}
