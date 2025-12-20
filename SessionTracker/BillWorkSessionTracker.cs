using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace EconomicSystem
{
    /// <summary>
    /// 记录 Pawn 在一次 Bill Job 中的工作量变化
    /// </summary>
    public static class BillWorkSessionTracker
    {
        private class BillWorkSession
        {
            public float workLeftAtStart;
        }

        // Pawn → 当前 Bill 工作会话
        private static readonly Dictionary<Pawn, BillWorkSession> sessions
            = new Dictionary<Pawn, BillWorkSession>();

        /// <summary>
        /// 开始记录一次 Bill 工作
        /// </summary>
        public static void Begin(Pawn pawn,Job job, float initialWorkLeft)
        {
            if (pawn == null || job?.bill == null)
                return;

            sessions[pawn] = new BillWorkSession
            {
                workLeftAtStart = initialWorkLeft
            };
        }

        /// <summary>
        /// 结束记录并返回实际完成的工作量
        /// </summary>
        public static float End(Pawn pawn,Job job, float initialWorkLeft)
        {
            if (pawn == null || job?.bill == null)
                return 0f;

            if (!sessions.TryGetValue(pawn, out var session))
                return 0f;
            
            sessions.Remove(pawn);

            float before = session.workLeftAtStart;
            float after =initialWorkLeft;
            
            //某些工作中断后,工作量清零(切石，烹饪等)
            //通过判断该工作是否会生成半成品来判断
            if (!(job.bill is Bill_ProductionWithUft productionBill ))
            {
                //剩余工作量大于0，代表没一次性做完。
                if (after>0)
                {
                    Log.Message("未一次性完成工作");
                    return 0f;
                }
                
            }
            
            //将tick转为更直观的工作量
            int delta = (int)(before - after)/60;
            // 安全处理：只接受正贡献
            return delta > 0 ? delta : 0;
        }
    }
}