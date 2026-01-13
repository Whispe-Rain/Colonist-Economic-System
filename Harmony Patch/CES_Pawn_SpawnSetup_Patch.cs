using HarmonyLib;
using Verse;

namespace EconomicSystem
{
    //为没有经济系统的殖民者初始化经济系统（一般用在开始新游戏时）
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
    public static class CES_Pawn_SpawnSetup_Patch
    {
        static void Postfix(Pawn __instance)
        {
            if (!__instance.Spawned)
                return;

            if (!__instance.IsColonist)
                return;

            __instance.EnsureEconomyData();
        }
    }

}