using Verse;

namespace EconomicSystem
{
    public static class CESUtility
    {
        ///pawn是否存在经济系统
        public static bool ShouldIntercept(Pawn pawn)
        {
            return pawn.TryGetEconomyData() != null;
        }
    }
}