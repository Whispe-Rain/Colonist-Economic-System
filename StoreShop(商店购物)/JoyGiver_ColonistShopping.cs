using System.Collections.Generic;
using Verse;
using Verse.AI;
using RimWorld;
using UnityEngine;
using System.Linq;
using RimWorld.Planet;

namespace EconomicSystem
{
    public class JoyGiver_ColonistShopping : JoyGiver
    {
        private const int CooldownTicks = 240; 
        private static readonly Dictionary<Pawn, int> lastTryTick = new();
        
        public override float GetChance(Pawn pawn)
        {
            //只对殖民者有效
            if (pawn==null||!pawn.IsColonist)
            {
                return 0f;
            }
            
            // 2. 经济检查
            var econ = pawn.GetEconomyData();
            // 检查虚拟余额是否足以进行任何购物
            if (econ == null || econ.virtualWallet < 50f) // 假设最低消费门槛为 50 银
            {
                return 0f;
            }

            // 3. 物品检查
            List<Thing> availableItems = ShoppingUtility.FindBuyableItemsInStockpiles(pawn);
            if (!availableItems.Any())
            {
                Log.Warning("没有可以购买的物品");
                return 0f;
            }

            // 4. 计算概率
    
            //基础购物概率2%
            float baseShoppingChance = 30f; 
    
            // 假设：在 15银时因子为 0.5，在 5000银时因子为 2.0 (您可以根据需要调整最大/最小影响)
            // Mathf.InverseLerp(min, max, value) 返回 0.0 到 1.0 之间的值
            float walletScale = Mathf.InverseLerp(50f, 2000f, econ.virtualWallet); 
    
            // 将 0~1 的 walletScale 映射到 0.5~2.0 的因子范围 (线性映射)
            float walletFactor = 0.5f + walletScale * 2f; 
    
            // 最终概率
            float finalChance = baseShoppingChance * walletFactor;
            
            Log.Warning("finalChance:"+finalChance);
            return finalChance; 
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            int now = Find.TickManager.TicksGame;

            if (lastTryTick.TryGetValue(pawn, out int last))
            {
                if (now - last < CooldownTicks)
                    return null;
            }
            lastTryTick[pawn] = now;
            
            
    
            // GetChance 已经确保了有物品和余额
            List<Thing> availableItems = ShoppingUtility.FindBuyableItemsInStockpiles(pawn);
            if (!availableItems.Any())
            {
                Log.Message($"[CES_DEBUG] Job denied: Re-check failed. No items available.");
                return null;
            }
            // --- 日志 1: 尝试触发 Job ---
            Log.Message($"[CES_DEBUG] {pawn.NameShortColored} is considering shopping...");
            // 迭代直到找到一个可以预定且能购买的物品
            foreach (Thing targetItem in availableItems)
            {
                // 1. 检查是否可以预定 (使用 ignoreOtherReservations = false, 这是默认值)
                if (!pawn.CanReserve(targetItem, 1, -1))
                {
                    continue; // 无法预定，尝试下一个物品
                }
        
                // 2.先创建 Job 实例
                Job newJob = JobMaker.MakeJob(this.def.jobDef, targetItem);

                // 3.使用新创建的 Job 实例进行预定
                // maxPawns=1, stackCount=-1 (预定整个堆栈)
                if (pawn.Reserve(targetItem, newJob, 1, -1)) 
                {
                    return newJob;
                }
            }
            return null;
        }
    }
}