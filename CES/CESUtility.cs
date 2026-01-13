using RimWorld;
using Verse;

namespace EconomicSystem
{
    public static class CESUtility
    {
        ///pawn是否存在经济系统且是殖民者
        public static bool ShouldIntercept(Pawn pawn)
        {
            // 必须有经济系统
            return pawn.TryGetEconomyData() != null;
        }
    }
}