using RimWorld;
using System.Collections.Generic;
using System.Linq;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace EconomicSystem
{
    // 假设您已经定义了 JobDefOf.CES_ForeignTrade
    [DefOf]
    public static class CES_JobDefOf
    {
        public static JobDef CES_ForeignTrade;
        
        static CES_JobDefOf() // 静态构造函数：Def 加载时执行
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(CES_JobDefOf));
        }
    }
    
    public class WorkGiver_ForeignTrade : WorkGiver_Scanner
    {
        // 交易的最远距离，防止追逐太远
        private const float MaxTradeDistance = 40f; 
        
        
        // !!! 【关键修正】：定义 WorkGiver 的扫描目标 !!!
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn) 
        {
            // 返回地图上所有 Pawn，让 JobOnThing 去筛选
            if (pawn.Map == null) 
                return Enumerable.Empty<Thing>();
            return pawn.Map.mapPawns.AllPawns.OfType<Thing>();
        }
        
        // 核心方法：尝试给 Pawn 分配一个 Job
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            // t 就是 PotentialWorkThingsGlobal 返回的 Pawn
            Pawn targetTrader = t as Pawn;
            if (targetTrader == null)
            {
                // 如果不是 Pawn，WorkGiver_Scanner 会跳过，但这里还是留个 Log 以防万一
                return null;
            }

            // ***** 强制 Log 依然保留，以确认 JobGiver_Work 正在调用这个类 *****
            Log.Error($"[CES GIVER INVOKED - WORKGIVER TEST] JobOnThing called for {pawn.NameShortColored}"); 

            // 1. 检查是否有待售的私人物品 (使用您原来的方法)
            CES_PawnEconomyData data = pawn.GetEconomyData();
            if (data == null || data.privateOwnedAssets.NullOrEmpty())
            {
                return null;
            }

            PrivateItemData itemToSell = GetRandomItemToSell(data.privateOwnedAssets);
            if (itemToSell == null)
            {
                return null;
            }

            // 2. 检查 Trader 是否有效 (使用您原来的方法)
            if (!IsViableTrader(pawn, targetTrader)) // <-- 调用新的辅助方法
            {
                return null;
            }

            // 3. 检查可达性 (WorkGiver 强制 CanReach)
            if (!pawn.CanReach(targetTrader, PathEndMode.OnCell, Danger.Some, canBashDoors: false, canBashFences: false))
            {
                return null;
            }

            // 4. 创建 Job
            Job job = JobMaker.MakeJob(CES_JobDefOf.CES_ForeignTrade, targetTrader);
            job.dutyTag = itemToSell.defName;
            Log.Message($"[CES] {pawn.NameShortColored} starting foreign trade WORK job with {targetTrader.NameShortColored}, selling: {itemToSell.defName}");

            return job;
        }

        // 辅助方法 1: 将您原来的 FindBestTraderPawn 逻辑的检查部分移到这里
        private bool IsViableTrader(Pawn forPawn, Pawn p)
        {
            const float MaxTradeDistance = 40f; 

            // ** 1. 核心检查：是否是真正的访客或商队 Pawn **
            // 检查 TraderKind 是否存在，这几乎是识别商队 Pawn 的唯一方法
            if (p.TraderKind == null)
            {
                // 排除掉所有没有 TraderKind 的普通访客 Pawn、囚犯、友方殖民者等。
                return false;
            }
    
            // ** 2. 基础状态和安全检查 **
            return p.Spawned // 必须在地图上
                   && !p.Downed // 不能倒地
                   && !p.HostileTo(forPawn) // 必须非敌对
                   && p.Faction != Faction.OfPlayer // 必须是非玩家派系
                   && p.IsFormingCaravan() == false // 排除正在组建大篷车的 Pawn
                   && p.IsPrisoner == false // 排除囚犯
                   // ** 3. 距离检查 **
                   && forPawn.Position.InHorDistOf(p.Position, MaxTradeDistance);
        }
        
        private PrivateItemData GetRandomItemToSell(List<PrivateItemData> items)
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
        
        
        
    }
}