using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace EconomicSystem
{
    /// <summary>
    /// 狩猎工作：按实际造成的伤害统计工作价值
    /// 挂钩到 Thing.TakeDamage 以确保所有伤害都被捕获。
    /// </summary>
    // 关键改变：目标方法改为 Thing.TakeDamage
    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))] 
    public static class Patch_Hunting_Damage
    {
        // 关键改变：Postfix 签名匹配 TakeDamage 的参数 (DamageInfo dinfo)
        // 注意：Thing.TakeDamage 也有一个可选的 __result (TakeDamageResult)
        // 但我们只需要 dinfo 和 victim (即 this 对象)
        static void Postfix(Thing __instance, DamageInfo dinfo)
        {
            // 1. 目标 (victim) 必须是动物
            Pawn animal = __instance as Pawn;
            // 确保动物还活着（虽然伤害发生时可能还没死，但我们在记录工作量时确保它是动物）
            if (animal == null || !animal.RaceProps.Animal) 
                return;

            // 2. Instigator 必须是 Pawn 且为殖民者
            Pawn hunter = dinfo.Instigator as Pawn;
            if (hunter == null || !hunter.IsColonist)
                return;
            
            // 3. 核心判断：确认殖民者正在执行狩猎 Job
            Job curJob = hunter.CurJob;
            if (curJob == null)
                return;
            
            // 狩猎工作的 JobDef 是 JobDefOf.Hunt
            if (curJob.def != JobDefOf.Hunt)
                return;

            // 4. 获取伤害量
            // 注意：dinfo.Amount 是原始伤害量，如果需要计算减伤后的“实际”伤害，
            // 事情会复杂化，但对于工作统计，使用原始伤害量通常足够。
            float damage = dinfo.Amount;
            if (damage <= 0f)
                return;

            // 5. 记录工作量
            var data = hunter.GetEconomyData();
            if (data == null)
                return;

            // 每次伤害都增加工作价值
            data.AddWork(WorkTypeDefOf.Hunting, damage);

            /*Log.Message(
                $"[CES] 狩猎 | {hunter.NameShortColored} | " +
                $"目标={animal.LabelShortCap} | 伤害={damage:F1}"
            );*/
        }
    }
}