using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace EconomicSystem
{
    public class JobGiver_ForeignSelling:ThinkNode_JobGiver
    {
       
        // 交易的最远距离，防止追逐太远
        private const float MaxTradeDistance = 40f; 
        
        
        protected override Job TryGiveJob(Pawn pawn)
        {
            //获得地图上所有可以交易的成员
            List<Pawn> viableTraders= pawn.Map.mapPawns.AllPawns
                .Where(p => ForeignUtility.IsViableTrader(pawn, p)) // 预先筛选
                .ToList();
            
            //随机选取一个作为交易目标
            Pawn targetTrader = viableTraders.RandomElement();
            if (targetTrader == null)
            {
                return null;
            }
            //如果该目标正在睡觉，忽略
            if (!targetTrader.Awake())
            {
                return null;
            }

            // 1. 检查是否有待售的私人物品 (使用您原来的方法)
            CES_PawnEconomyData data = pawn.GetEconomyData();
            if (data == null || data.privateOwnedAssets.NullOrEmpty())
            {
                return null;
            }

            
            //交易的物品是否为空
            PrivateItemData itemToSell =ForeignUtility.GetRandomItemToSell(data.privateOwnedAssets);
            if (itemToSell == null)
            {
                return null;
            }
            
            
            string cdKey = $"ForeignTrade:{itemToSell.defName}";
            int now = Find.TickManager.TicksGame;
            // 该物品的交易是否在CD 中？
            if (data.coolDowns.TryGetValue(cdKey, out int untilTick))
            {
                if (now < untilTick)
                {
                    return null;
                }
                    
            }
            // 2. 检查 Trader 是否有效
            if (!ForeignUtility.IsViableTrader(pawn, targetTrader)) // <-- 调用新的辅助方法
            {
                return null;
            }

            // 3. 检查可达性 (WorkGiver 强制 CanReach)
            if (!pawn.CanReach(targetTrader, PathEndMode.OnCell, Danger.Some, canBashDoors: false, canBashFences: false))
            {
                return null;
            }
            
            //防止追逐太远
            if ((pawn.Position - targetTrader.Position).LengthHorizontalSquared > MaxTradeDistance * MaxTradeDistance)
                return null;
            
            // 4. 创建 Job
            Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("CES_ForeignSelling"), targetTrader);
            job.dutyTag = itemToSell.defName;
            Log.Message($"[CES] {pawn.NameShortColored}开始从事对外贸易工作:对{targetTrader.NameShortColored},出售:{itemToSell.defName}");
            return job;
        }
    }
}