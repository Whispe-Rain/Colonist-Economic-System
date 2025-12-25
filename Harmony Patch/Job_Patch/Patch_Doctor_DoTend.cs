using HarmonyLib;
using RimWorld;
using Verse;

namespace EconomicSystem
{
    /// <summary>
    /// 医疗工作统计
    /// 按 Tend 质量计算工作价值
    /// </summary>
    [HarmonyPatch(typeof(TendUtility), nameof(TendUtility.DoTend))]
    public static class Patch_Doctor_DoTend
    {
        static void Postfix(Pawn doctor, Pawn patient, Medicine medicine)
        {
            if (doctor == null || !doctor.IsColonist)
                return;

            // 1. 重新计算 DoTend 内部使用的基础治疗质量
            // 这是医生这次治疗效果的主要决定因素
            float baseTendQuality = TendUtility.CalculateBaseTendQuality(doctor, patient, medicine?.def); 

            // 在 RimWorld 中， baseTendQuality 是一个 0.0 到 MedicalQualityMax (通常是 0.7 到 1.8) 之间的值，
            // 直接代表了这次治疗提供的“医疗量”
            float tendQualityForWork = baseTendQuality; 

            // 假设我们用治疗质量作为工作量
            float workValue = tendQualityForWork; 

            var data = doctor.GetEconomyData();
            if (data == null)
                return;

            // 使用计算出的工作价值
            data.AddWork(WorkTypeDefOf.Doctor, workValue);

            /*Log.Message(
                $"[CES] 医疗工作完成 | 医生={doctor.NameShortColored} | 工作价值={workValue:F2}"
            );*/
        }
    }
}