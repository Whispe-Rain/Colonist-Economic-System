using System.Runtime.CompilerServices;
using Verse;

//角色经济扩展
//为Pawn增加了经济数据的扩展方法
namespace EconomicSystem
{
    public static class CES_PawnEconomyExtension
    {
        //得到Pawn的经济数据
        public static CES_PawnEconomyData TryGetEconomyData(this Pawn pawn)
        {
            return Current.Game
                .GetComponent<CES_EconomyGameComponent>()
                ?.TryGetEconomyData(pawn);
        }
        
        // 明确创建（仅在“入队/入殖民地”事件中使用）
        public static CES_PawnEconomyData EnsureEconomyData(this Pawn pawn)
        {
            var comp = Current.Game.GetComponent<CES_EconomyGameComponent>();
            return comp?.CreateOrEnableEconomyDataFor(pawn);
        }
        
    }
    

    

    
}