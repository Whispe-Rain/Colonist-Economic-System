using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace EconomicSystem
{
    public class JobGiver_ForeignTrade:ThinkNode_JobGiver
    {
        //冷却时间
        private const int CooldownTicks = 600;
        private static readonly Dictionary<Pawn, int> lastTryTick = new();
        // 交易的最远距离，防止追逐太远
        private const float MaxTradeDistance = 40f; 
        
        
        protected override Job TryGiveJob(Pawn pawn)
        {
            //冷却
            int now = Find.TickManager.TicksGame;

            if (lastTryTick.TryGetValue(pawn, out int last))
            {
                if (now - last < CooldownTicks)
                    return null;
            }
            lastTryTick[pawn] = now;
            
            //获得地图上所有可以交易的成员
            List<Pawn> viableTraders= pawn.Map.mapPawns.AllPawns
                .Where(p => ForeignTradeUtility.IsViableTrader(pawn, p)) // 预先筛选
                .ToList();
            
            //随机选取一个作为交易目标
            Pawn targetTrader = viableTraders.RandomElement();
            if (targetTrader == null)
            {
                // 如果不是 Pawn，WorkGiver_Scanner 会跳过，但这里还是留个 Log 以防万一
                return null;
            }

            // 1. 检查是否有待售的私人物品 (使用您原来的方法)
            CES_PawnEconomyData data = pawn.GetEconomyData();
            if (data == null || data.privateOwnedAssets.NullOrEmpty())
            {
                return null;
            }

            PrivateItemData itemToSell =ForeignTradeUtility.GetRandomItemToSell(data.privateOwnedAssets);
            if (itemToSell == null)
            {
                return null;
            }

            // 2. 检查 Trader 是否有效 (使用您原来的方法)
            if (!ForeignTradeUtility.IsViableTrader(pawn, targetTrader)) // <-- 调用新的辅助方法
            {
                return null;
            }

            // 3. 检查可达性 (WorkGiver 强制 CanReach)
            if (!pawn.CanReach(targetTrader, PathEndMode.OnCell, Danger.Some, canBashDoors: false, canBashFences: false))
            {
                return null;
            }
            
            // 4. 创建 Job
            Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("CES_ForeignTrade"), targetTrader);
            job.dutyTag = itemToSell.defName;
            Log.Message($"[CES] {pawn.NameShortColored} starting foreign trade WORK job with {targetTrader.NameShortColored}, selling: {itemToSell.defName}");
            return job;
        }
    }
}