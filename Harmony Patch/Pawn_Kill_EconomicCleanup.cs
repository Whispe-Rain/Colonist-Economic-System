using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace EconomicSystem
{
    //当小人死亡时将身上的虚拟物品全部转为实体掉落
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Pawn_Kill_EconomicCleanup
    {
        // Prefix 在原版 Pawn.Kill 运行之前执行。
        // 返回 false 将阻止原版 Kill 方法运行。
        // 返回 true 将允许原版 Kill 方法继续运行。
        public static bool Prefix(Pawn __instance)
        {
            // 1. 只处理殖民者或您定义的经济实体
            // 避免处理小动物、野生动物或不相关的 NPC。
            if (!__instance.IsColonist && !__instance.RaceProps.Humanlike)
            {
                return true;
            }

            // 2. 检查 Pawn 是否已经死亡 (避免重复触发，但 Kill 内部通常会处理)
            if (__instance.Dead)
            {
                return true;
            }

            // 3. 执行核心清理逻辑
            TransferAssetsOnDeath(__instance);

            // 4. 继续执行原版 Kill 逻辑 (让 Pawn 真正死亡)
            return true;
        }

        // 核心清理逻辑：将虚拟资产转移为实体物品
        private static void TransferAssetsOnDeath(Pawn pawn)
        {
            CES_PawnEconomyData data = pawn.GetEconomyData();

            if (data == null)
            {
                return;
            }

            // --- A. 处理虚拟钱包 ---
            int moneyToDrop = Mathf.FloorToInt(data.virtualWallet);

            if (moneyToDrop > 0)
            {
                // 1. 尝试清空虚拟钱包
                data.SubtractMoney(moneyToDrop); 

                // 2. 生成实体银子
                Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                silver.stackCount = moneyToDrop;
                
                // 3. 掉落在 Pawn 死亡的位置
                GenSpawn.Spawn(silver, pawn.Position, pawn.Map);
            }
            
            // --- B. 处理私人物品 ---
            if (data.privateOwnedAssets.Count > 0)
            {
                // 遍历并具象化所有私人物品
                foreach (PrivateItemData itemData in data.privateOwnedAssets.ToList()) // ToList() 避免在迭代中修改列表
                {
                    // 1. 将虚拟数据具象化为实体 Thing
                    Thing actualItem = itemData.RecreateThing(); 

                    if (actualItem != null)
                    {
                        
                        // 2. 掉落在 Pawn 死亡的位置
                        GenSpawn.Spawn(actualItem, pawn.Position, pawn.Map);
                    }
                    
                    // 3. 从虚拟列表中移除（防止尸体携带虚拟资产）
                    data.privateOwnedAssets.Remove(itemData);
                }
            }
            Log.Message($"[CES] {pawn.NameShortColored} 死亡，成功掉落 {moneyToDrop} 白银和 {data.privateOwnedAssets.Count} 件私人物品。");
        }
    }
}