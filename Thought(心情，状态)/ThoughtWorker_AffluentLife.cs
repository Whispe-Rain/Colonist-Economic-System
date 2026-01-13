// 文件: ThoughtWorker_AffluentLife.cs

using RimWorld;
using Verse;
using System.Linq;

namespace EconomicSystem
{
    public class ThoughtWorker_AffluentLife : ThoughtWorker
    {
        
        
        // 定义相对财富的百分比系数（作为平衡性调整的中心点）
        // 阈值 1 (平衡): 殖民地白银的 5%
        private const float Factor_Stable = 0.05f; 
        // 阈值 2 (富足): 殖民地白银的 10%
        private const float Factor_Affluent = 0.1f; 
        // 阈值 3 (自由): 殖民地白银的 20%
        private const float Factor_Wealthy = 0.2f; 
        
        

        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            
            if (!p.IsColonistPlayerControlled || p.Map == null)
            {
                return ThoughtState.Inactive;
            }

            var data = p.TryGetEconomyData();
            if (data == null||!data.active)
            {
                return ThoughtState.Inactive;
            }

            float wallet = data.virtualWallet;
            
            // 1. 获取殖民地硬通货总量
            float colonySilver = CES_EconomyUtility.GetColonySilverTotal(p.Map);
            
            // 2. 计算动态阈值
            float thresholdWealthy = colonySilver * Factor_Wealthy;
            float thresholdAffluent = colonySilver * Factor_Affluent;
            float thresholdStable = colonySilver * Factor_Stable;

            // 3. 判断并返回对应的 Stage 索引
            // Stages: [0] 贫困 (-5), [1] 平衡 (0), [2] 富足 (+8), [3] 自由 (+15)

            if (wallet >= thresholdWealthy)
            {
                // 财富自由 (Stage 3)
                return ThoughtState.ActiveAtStage(3);
            }
            if (wallet >= thresholdAffluent)
            {
                // 生活富足 (Stage 2)
                return ThoughtState.ActiveAtStage(2);
            }
            if (wallet >= thresholdStable) 
            {
                // 收支平衡 (Stage 1) 
                return ThoughtState.ActiveAtStage(1);
            }
            
            // 生活贫困 (Stage 0)
            return ThoughtState.ActiveAtStage(0);
        }
    }
}