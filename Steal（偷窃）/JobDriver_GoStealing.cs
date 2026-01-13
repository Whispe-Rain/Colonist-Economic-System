using System.Collections.Generic;
using Gastronomy;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace EconomicSystem
{
    public class JobDriver_GoStealing : JobDriver
    {
        // Toil 的 Job 持续时间（Ticks）
        private const int TicksToChat = 900;

        // 用于获取目标Pawn 的快捷方法
        private Pawn TargetPawn => (Pawn)job.GetTarget(TargetIndex.A).Thing;

        //预约目标
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            LocalTargetInfo targetPawn = job.GetTarget(TargetIndex.A);

            var econ = pawn.TryGetEconomyData();
            if (econ == null)
            {
                return false;
            }

            //⭐ 核心修正：直接尝试预定目标 Pawn
            // 预定参数：目标Pawn，当前Job，预定数量(1)，堆叠(-1)，错误标志(errorOnFailed)
            bool reserved = this.pawn.Reserve(targetPawn, this.job, 1, -1, null, errorOnFailed);
            if (reserved)
            {
                Log.Message(
                    $"[CES_DEBUG] JobDriver Start: {pawn.NameShortColored} 成功预定 {TargetPawn.NameShortColored}。");
            }
            else
            {
                Log.Warning(
                    $"[CES_DEBUG] JobDriver denied: {pawn.NameShortColored} 无法预定 {TargetPawn.NameShortColored}。");
            }

            return reserved;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // 失败条件: 如果Pawn死亡或离开地图,或者Pawn醒来
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOnDowned(TargetIndex.A);
            this.FailOn(() => this.pawn.Drafted);

            // --- Toil 1: 前往受害人（Goto） ---
            Toil goToTrader = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch)
                .FailOn(() => !TargetPawn.Spawned); // 目标Pawn必须在地图上

            yield return goToTrader;

            // --- Toil 2: 开始执行偷窃行为（大概15秒),期间不断左顾右盼 ---
            Toil interaction = Toils_General.Wait(TicksToChat, TargetIndex.A)
                .WithProgressBarToilDelay(TargetIndex.A);

            interaction.tickAction = delegate
            {
                // 旋转：确保 Pawn 随机环顾四周
                if (Find.TickManager.TicksGame % 60 == 0)
                {
                    this.pawn.Rotation = Rot4.Random;
                }
            };
            yield return interaction;

            // --- Toil 3: 偷窃物品或白银 Toil（Do） ---
            yield return Toils_General.Do(delegate
            {
                // 获取Pawn和Target的经济数据
                CES_PawnEconomyData data = pawn.TryGetEconomyData();
                CES_PawnEconomyData enco = TargetPawn.TryGetEconomyData();

                // 任意一方不存在经济数据，中断
                if (data == null || enco == null)
                {
                    Log.Warning($"[CES] {pawn.NameShortColored} failed trade: Missing economy data or ItemDefName.");
                    return;
                }

                // 尝试执行交易逻辑
                StealableItem(enco, data);
                
            });
        }


        // 偷窃目标（物品或白银）
        private void StealableItem(CES_PawnEconomyData enco, CES_PawnEconomyData data)
        {
            //先判断这次偷窃是否成功
            //计算概率(一个基础概率+双方战斗能力的差值*0.005)
            float probability = Rand.Range(0.3f, 0.5f)
                                + (pawn.skills.GetSkill(SkillDefOf.Shooting).Level -
                                   TargetPawn.skills.GetSkill(SkillDefOf.Shooting).Level) * 0.005f
                                + (pawn.skills.GetSkill(SkillDefOf.Shooting).Level -
                                   TargetPawn.skills.GetSkill(SkillDefOf.Melee).Level) * 0.005f;
            if (Rand.Chance(probability))
            {
                //判断是偷钱还是偷东西
                float isStealItem = Rand.Range(0f, 1f);
                switch (isStealItem)
                {
                    //偷窃白银
                    case <= 0.5f:
                        float stealFactor = Rand.Range(0.05f, 0.15f) +
                                            pawn.skills.GetSkill(SkillDefOf.Shooting).Level * 0.005f +
                                            pawn.skills.GetSkill(SkillDefOf.Melee).Level * 0.005f;
                        int stealSilver = (int)(enco.virtualWallet *stealFactor);

                        enco.virtualWallet -= stealSilver;
                        string history = $"钱包被盗,损失{stealSilver.ToString().Colorize(Color.red)},".Colorize(Color.white);
                        enco.AddHistory(enco.economicHistory,history);
                        
                        data.virtualWallet += stealSilver;
                        string history2 = $"从{TargetPawn.LabelShort}手中盗窃了白银{stealSilver.ToString().Colorize(Color.green)}".Colorize(Color.white);
                        data.AddHistory(data.economicHistory,history2);
                        
                        break;
                    //偷窃物品
                    case >= 0.5f:
                        PrivateItemData thing = enco.privateOwnedAssets.RandomElement();
                        //从受害者背包中移除该物品
                        enco.RemoveAsset(thing);
                        string history3 = $"{thing.Name.Colorize(Color.red)}被盗,".Colorize(Color.white);
                        enco.AddHistory(enco.economicHistory,history3);
                        
                        //加入到偷窃者的背包中
                        data.privateOwnedAssets.Add(thing);
                        string history4 = $"成功盗窃{thing.Name.Colorize(Color.green)},".Colorize(Color.white);
                        data.AddHistory(data.economicHistory,history4);
                        break;
                }

                ApplyVictimMoodLoss(TargetPawn,pawn);
            }
            else
            {
                //盗窃失败
                ApplyThiefMoodLoss(pawn, TargetPawn);
                //受害者立刻醒来
                TargetPawn.jobs.EndCurrentJob(JobCondition.Incompletable, true);
                //扣好感度
                ApplyOpinionLoss(pawn, TargetPawn);

            }
            
            
        }
        // 2. 降低偷窃者心情 (偷窃失败)
        private void ApplyThiefMoodLoss(Pawn thief, Pawn victim)
        {
            if (thief.needs?.mood != null)
            {
                thief.needs.mood.thoughts.memories.TryGainMemory(
                    DefDatabase<ThoughtDef>.GetNamed("CES_CaughtStealing"), 
                    victim // 传入受害者作为关联Pawn
                );
            }
        }
        //降低受害者心情
        private void ApplyVictimMoodLoss(Pawn victim,Pawn thief)
        {
            if (victim.needs?.mood != null)
            {
                victim.needs.mood.thoughts.memories.TryGainMemory(
                    DefDatabase<ThoughtDef>.GetNamed("CES_WasRobbed"), 
                    thief // 传入受害者作为关联Pawn
                );
            }
        }
        
        // 降低好感度 (偷窃失败)
        private void ApplyOpinionLoss(Pawn thief, Pawn victim)
        {
            // 1. 受害者心情和好感度降低：
            //    添加一个记忆 (CES_CaughtThief)，它在 XML 中定义了对偷窃者的负面 opinionOffset。
            if (victim.needs?.mood != null)
            {
                victim.needs.mood.thoughts.memories.TryGainMemory(
                    DefDatabase<ThoughtDef>.GetNamed("CES_CaughtThief"), // 受害者记住偷窃者
                    thief // 传入偷窃者作为关联Pawn
                );
            }

            // 2. 偷窃者心情和好感度降低：
            //    添加一个记忆 (CES_CaughtStealing)，它在 XML 中定义了对受害者的负面 opinionOffset (可选)。
            if (thief.needs?.mood != null)
            {
                thief.needs.mood.thoughts.memories.TryGainMemory(
                    DefDatabase<ThoughtDef>.GetNamed("CES_CaughtStealing"), // 偷窃者感到尴尬/羞耻
                    victim // 传入受害者作为关联Pawn
                );
            }
        }
    }
}