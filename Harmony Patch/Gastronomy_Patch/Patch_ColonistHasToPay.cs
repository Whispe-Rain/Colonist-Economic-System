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
            if (CESUtility.ShouldIntercept(patron))
                return ;

            // CES Pawn：我们接管“是否付费”的定义
            __result = true;
        }
    }
}