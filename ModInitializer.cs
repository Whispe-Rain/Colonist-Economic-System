using HarmonyLib;
using RimWorld;
using Verse;
using System;
using System.Collections.Generic;
using System.Linq; // 需要 Linq 命名空间

namespace EconomicSystem
{
    //动态注入xml,实现ITab_Pawn_Economy
    [StaticConstructorOnStartup]
    public static class ModInitializer
    {
        static ModInitializer()
        {
            // 在静态构造函数中执行注入逻辑
            TryInjectPawnEconomyTabToHumanlike();
        }

        private static void TryInjectPawnEconomyTabToHumanlike()
        {
            // 找到所有 Humanlike 的 ThingDef
            foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefsListForReading
                         .Where(def => def.race != null && def.race.Humanlike))
            {
                Type tabType = typeof(ITab_Pawn_Economy);

                // 1. 检查 ITab 是否已经存在 (防止重复注册)
                if (thingDef.inspectorTabs.Contains(tabType))
                {
                    continue; // 已存在，跳过
                }
                
                // 2. 注入 ITab Type
                thingDef.inspectorTabs.Add(tabType);

                // 3. 注入 ITab 的共享实例 (Resolved) - 确保运行时流畅
                // 仅当 inspectorTabsResolved 尚未被初始化时，才需要重新创建列表
                if (thingDef.inspectorTabsResolved == null)
                {
                    thingDef.inspectorTabsResolved = new List<InspectTabBase>();
                }
                thingDef.inspectorTabsResolved.Add(InspectTabManager.GetSharedInstance(tabType));
                
            }
            Log.Message("[CES] PawnEconomy ITab 成功注入到所有 Humanlike 种族。");
        }
    }
}