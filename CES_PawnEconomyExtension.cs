using System.Runtime.CompilerServices;
using Verse;

//角色经济扩展
//为Pawn增加了经济数据的扩展方法
namespace EconomicSystem
{
    public static class CES_PawnEconomyExtension
    {
        //得到Pawn的经济数据
        public static CES_PawnEconomyData GetEconomyData(this Pawn pawn)
        {
            if (pawn == null || !pawn.IsColonist)
                return null;

            return Current.Game
                .GetComponent<CES_EconomyGameComponent>()
                ?.GetOrCreateDataFor(pawn);
        }
        
    }
}