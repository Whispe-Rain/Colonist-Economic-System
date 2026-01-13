using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace EconomicSystem
{
    public class JobGiver_StealFromPawn : ThinkNode_JobGiver
    {
        private const int CooldownTicks = 2500; // ~1小时
        private static readonly Dictionary<Pawn, int> lastTryTick = new();
        protected override Job TryGiveJob(Pawn pawn)
        {
            
            Log.Message($"[Steal] JobGiver called for {pawn}");
            //得到地图上全部角色
            List<Pawn> victims = pawn.Map.mapPawns.AllHumanlike;
            
            int now = Find.TickManager.TicksGame;
            //得到满足条件的受害者
            Pawn victim = GetVictim(victims);
            
            if (lastTryTick.TryGetValue(pawn, out int last))
            {
                if (now - last < CooldownTicks)
                    return null;
            }
            lastTryTick[pawn] = now;
            
            // 创建任务并使用上面定义的 JobDef
            Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("CES_GoStealing"), victim);
            return job;
        }
        
        
        
      // T找到一个合适的偷窃目标（物品或另一个 Pawn 的资源）
        private Thing FindStealableItem(Pawn pawn)
        {
            
            return null; 
        }

        
        private Pawn GetVictim(List<Pawn> victims)
        {
            List<Pawn> victimsCopy = victims.ToList();
            
            List<Pawn> validVictims = new List<Pawn>();

            foreach (Pawn victim in victimsCopy)
            {
                if (victim == null) continue;
                
                //睡眠检测
                if (victim.Awake())
                {
                   continue;
                }

                // 身份与状态检查
                if (victim.Dead || !victim.IsColonist || victim.IsPrisoner || victim.Awake())
                    continue;

                var econ = victim.TryGetEconomyData();
                if (econ == null) continue;

                // 财富检查
                if (econ.virtualWallet <= 100f)
                    continue;

                if (econ.privateOwnedAssets == null || econ.privateOwnedAssets.Count <= 0)
                    continue;

                validVictims.Add(victim);
            }
            
            if (validVictims.Count > 0)
            {
                return validVictims.RandomElement();
            }
            
            return null;
        }

    }
}
