using System;
using RimWorld;
using Verse;

namespace EconomicSystem
{
    /// <summary>
    /// 欠薪状态
    /// </summary>
    public class ThoughtWorker_UnpaidWage : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            try
            {
                // 检查基础条件
                if (p == null || !p.IsColonist)
                    return ThoughtState.Inactive;
                
                var data = p.TryGetEconomyData(); 

                if (data == null || !data.active)
                {
                    return ThoughtState.Inactive;
                }

                // 3. 检查数据字段
                float unpaid = data.unpaidWage;

                if (unpaid <= 0f)
                    return ThoughtState.Inactive;

                // 5. 阶段判断逻辑（如果前面的都没问题，这里是正确的）
                if (unpaid > data.GetUnpaidWageLevel(2))
                    return ThoughtState.ActiveAtStage(2);

                if (unpaid > data.GetUnpaidWageLevel(1))
                    return ThoughtState.ActiveAtStage(1);

                return ThoughtState.ActiveAtStage(0);
            }
            catch (Exception ex)
            {
                // 💥 捕获并强制打印异常！
                Log.Error($"CES Debug ERROR: ThoughtWorker_UnpaidWage 运行时异常: {ex.Message}\n堆栈追踪: {ex.StackTrace}");
                return ThoughtState.Inactive;
            }
        }
    }
}