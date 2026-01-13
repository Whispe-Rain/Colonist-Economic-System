using RimWorld;
using Verse;
using UnityEngine; // For Mathf and GenPlace

namespace EconomicSystem
{
    // 核心：将银币存入殖民地 (Pawn -> Map)
    public static class EconomyUtility
    {
        /// <summary>
        /// Pawn 将银币存入殖民地 (从 Pawn 钱包取出，在地图上生成银币堆)
        /// </summary>
        public static bool TryReturnSilverToColony(Pawn pawn, float amount)
        {
            if (amount <= 0 || pawn == null || pawn.Map == null) return true;

            int intAmount = Mathf.FloorToInt(amount);
            CES_PawnEconomyData data = pawn.TryGetEconomyData();
            
            if (data == null || data.virtualWallet < intAmount)
            {
                // 如果钱包余额不足，则无法存回
                Log.Warning($"[CES] {pawn.Name.ToStringShort} 钱包余额不足({data?.virtualWallet})以存入 {intAmount} 银币。");
                return false;
            }

            // 1. 减少 Pawn 的钱包余额
            data.virtualWallet -= intAmount;

            // 2. 在地图上生成银币堆
            Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver, null);
            silver.stackCount = intAmount;

            // 尝试将银币生成在 Pawn 所在位置或附近
            // GenPlace 确保了物体被放在一个合理的位置
            if (GenPlace.TryPlaceThing(silver, pawn.Position, pawn.Map, ThingPlaceMode.Near))
            {
                // Log.Message($"[CES] {pawn.Name.ToStringShort} 成功存入 {intAmount} 银币到殖民地。");
                return true;
            }
            else
            {
                // 极少情况：地图上没有空间放银币
                Log.Error($"[CES] 无法在 {pawn.Name.ToStringShort} 附近放置银币。请检查地图空间。");
                // 尝试回退余额
                data.virtualWallet += intAmount; 
                return false;
            }
        }
    }
}