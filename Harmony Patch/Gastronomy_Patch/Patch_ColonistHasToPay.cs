using HarmonyLib;
using Verse;
using EconomicSystem;
using Gastronomy.Dining;
using RimWorld;

namespace EconomicSystem
{
    /// <summary>
    /// 允许殖民者付费
    /// </summary>
    [HarmonyPatch(typeof(DiningUtility), nameof(DiningUtility.HasToPay))]
    public static class Patch_ColonistHasToPay
    {
        // Postfix 在原方法执行后运行
        // __result 是原方法的返回值 (bool)
        public static void Postfix(Pawn patron, ref bool __result)
        {
            // 1. 如果原始结果已经是 true (例如，它本身就是访客)，则不需要修改
            if (__result)
            {
                return;
            }

            // 2. 如果是殖民者，并且我们的经济系统已激活，则强制返回 true
            if (patron.IsColonist && patron.Faction == Faction.OfPlayer)
            {
                // 我们只需要让殖民者拥有经济数据，就认为他们需要付费
                if (patron.GetEconomyData() != null)
                {
                    __result = true;
                }
            }
        }
    }
}