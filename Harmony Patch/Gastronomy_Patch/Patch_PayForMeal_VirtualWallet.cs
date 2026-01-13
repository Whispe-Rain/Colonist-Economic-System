using HarmonyLib;
using RimWorld;
using Verse;
using EconomicSystem;
using System;
using System.Linq;
using UnityEngine;
using Gastronomy.Restaurant;
using Gastronomy.Dining; // 包含 DiningUtility 的命名空间

namespace EconomicSystem
{
    // 使用 Harmony Patch 标记整个类
    [HarmonyPatch(typeof(DiningUtility), nameof(DiningUtility.PayForMeal))]
    public static class Patch_PayForMeal_VirtualWallet
    {
        // PayForMeal 的签名: public static void PayForMeal(this Pawn pawn, ThingOwner payTarget, out Thing paidSilver)
        // Harmony 参数对应: __0 = pawn, __1 = payTarget, __2 = paidSilver

        public static bool Prefix(Pawn __0, ThingOwner __1, out Thing __2)
        {
            Pawn pawn = __0;
            ThingOwner payTarget = __1;
            
            __2 = null; // 初始化 out 参数 paidSilver

            // 1. 检查是否拥有经济数据
            CES_PawnEconomyData data = pawn.TryGetEconomyData();
            
            if (!CESUtility.ShouldIntercept(pawn))
                return true;
            
            // 2. 获取餐厅和待支付金额 (必须重新执行原始方法的 LINQ 查询逻辑)
            // 原始代码:
            // var <>f__AnonymousType = (from r in pawn.GetAllRestaurants() select new { restaurant = r, debt = r.Debts.GetDebt(pawn) }).FirstOrDefault(d => d.debt != null);
            
            // 找到包含债务的餐厅
            var restaurantDebt = pawn.GetAllRestaurants()
                .Select(r => new { restaurant = r, debt = r.Debts.GetDebt(pawn) })
                .FirstOrDefault(d => d.debt != null);

            if (restaurantDebt == null)
            {
                return true; // 没有待支付债务，让原始方法继续 (虽然原始方法会直接返回)
            }
            
            // 待支付金额
            float requiredAmountF = restaurantDebt.debt.amount;
            int requiredAmount = Mathf.FloorToInt(requiredAmountF);

            if (requiredAmount <= 0)
            {
                return true; // 债务为零或负数，让原始方法继续
            }

            // 3. 检查虚拟钱包余额
            if (data.virtualWallet >= requiredAmount)
            {
                
                // 1. 扣除虚拟钱包
                data.virtualWallet -= requiredAmount;

                // 2. 创建实体银币
                Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver, null);
                silver.stackCount = requiredAmount;

                // 3. 将银币转移到 payTarget (收银台的库存或其他目标)
                if (payTarget.TryAdd(silver))
                {
                    // 4. 模拟成功支付：设置 paidSilver (out) 和支付债务
                    __2 = silver; 
                    restaurantDebt.restaurant.Debts.PayDebt(pawn, requiredAmount);

                    Log.Message($"[CES] {pawn.Name.ToStringShort} 支付了 {requiredAmount} 银币餐费。");
                    
                    // 5. 阻止原始方法执行
                    return false; 
                }
                else
                {
                    // 转移失败 (payTarget 拒绝接收等，极少发生)
                    Log.Error($"[CES] 虚拟钱包支付失败：无法将银币转入 {payTarget}。回退余额。");
                    data.virtualWallet += requiredAmount; // 回退
                    return true; // 运行原始方法，可能会失败
                }
            }
            
            // 4. 虚拟钱包不足，让原始方法继续执行 (它会尝试使用实体银币，如果实体银币也失败，那么支付彻底失败)
            return true;
        }
    }
}