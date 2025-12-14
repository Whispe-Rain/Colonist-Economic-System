using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace EconomicSystem
{
    /// <summary>
    /// 负责将 RimWorld 的 Job 转换为一个“工作价值”。
    ///
    /// 设计目标：
    /// - 不依赖 Tick
    /// - 不保存状态
    /// - 仅在 Job 完成时调用
    ///
    /// 返回值是一个抽象劳动量，用于后续工资/统计计算。
    /// </summary>
    public static class WorkValueCalculator
    {
        /// <summary>
        /// 计算某个 Job 的工作价值。
        /// </summary>
        /// <param name="job">已完成的 Job</param>
        /// <returns>工作价值（>=0）</returns>
        public static float Calculate(Job job)
        {
            if (job == null)
                return 0f;

            // 1️⃣ 研究类工作
            if (TryCalculateResearch(job, out float researchValue))
                return researchValue;

            // 2️⃣ 制作 / 锻造 / 缝纫 / 艺术等 Bill 类工作
            if (TryCalculateBillWork(job, out float billValue))
                return billValue;

            // 3️⃣ 其他离散型工作
            return DefaultDiscreteWorkValue(job);
        }
        public static WorkLaborModel GetLaborModel(ToilCompleteMode mode)
        {
            return mode switch
            {
                ToilCompleteMode.Delay => WorkLaborModel.TimeBased,
                ToilCompleteMode.Never => WorkLaborModel.SkillBased,
                _ => WorkLaborModel.None
            };
        }


        /// <summary>
        /// 尝试按“研究工作”计算价值。
        /// 研究的劳动量由 ResearchProjectDef.baseCost 决定。
        /// </summary>
        private static bool TryCalculateResearch(Job job, out float value)
        {
            value = 0f;

            // 判断是否为研究工作
            if (job.def != JobDefOf.Research)
                return false;

            // 1.6 正确获取当前研究项目（通过官方方法）
            ResearchProjectDef project =
                Find.ResearchManager?.GetProject();

            if (project == null)
                return false;

            /*
             * baseCost 代表该研究的理论规模
             * 我们将其映射为一次“研究劳动”的经济价值
             *
             * 注意：
             * - 不是研究进度
             * - 不是 tick
             * - 是经济系统内部使用的抽象值
             */
            const float RESEARCH_SCALE = 0.005f;

            value = project.baseCost * RESEARCH_SCALE;
            return true;
        }


        /// <summary>
        /// 尝试按 Bill / Recipe 工作计算价值。
        /// 适用于制作、艺术、锻造、缝纫等。
        /// </summary>
        private static bool TryCalculateBillWork(Job job, out float value)
        {
            value = 0f;

            // 只有带 Bill 的 Job 才有 Recipe
            if (job.bill?.recipe == null)
                return false;

            RecipeDef recipe = job.bill.recipe;

            // workAmount 是该配方的理论劳动量
            // 与 Pawn 技能、速度无关
            float workAmount = recipe.workAmount;

            if (workAmount <= 0f)
                return false;

            const float BILL_WORK_SCALE = 0.01f;

            value = workAmount * BILL_WORK_SCALE;
            return true;
        }

        /// <summary>
        /// 默认离散型工作的价值。
        /// </summary>
        private static float DefaultDiscreteWorkValue(Job job)
        {
            // 这里返回 1，代表“一次完整贡献”
            // 以后可以按 WorkType 或 JobDef 再细分
            return 1f;
        }
        // 记录：Pawn → 上一次统计的 Tick
        private static readonly Dictionary<Pawn, int> lastRecordedTick
            = new Dictionary<Pawn, int>();

        /// <summary>
        /// 判断该 Pawn 在当前 Tick 是否还能记录劳动
        /// 用于防止同一 Tick 内重复计数
        /// </summary>
        public static bool CanRecordThisTick(Pawn pawn)
        {
            int currentTick = Find.TickManager.TicksGame;

            if (lastRecordedTick.TryGetValue(pawn, out int lastTick))
            {
                if (lastTick == currentTick)
                {
                    // 已经在这个 Tick 统计过
                    return false;
                }
            }

            // 记录本次 Tick
            lastRecordedTick[pawn] = currentTick;
            return true;
        }

        public static void NotifyWorkTick(Pawn pawn, Job job)
        {
            // 暂时只记录
            // 每 60 tick 记一次，避免性能问题
            if (Find.TickManager.TicksGame % 60 != 0)
                return;

            Log.Message(
                $"[WorkValue] {pawn.LabelShort} working on {job.def.defName}"
            );
        }
    }
}