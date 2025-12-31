using System;
using Verse;
using Verse.AI;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using System.Linq;
using Hospitality.Utilities;

namespace EconomicSystem
{
    public class JobDriver_GoShopping : JobDriver
    {
        // Toil 的 Job 持续时间（Ticks）
        private const int TicksToBrowse = 600;
        private const int TicksToPay = 90;

        private Thing TargetItem => TargetA.Thing;
        
        
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            var econ = pawn.GetEconomyData();
            if (econ == null)
            {
                return false;
            }
            // 检查预定是否仍然有效（主要是防止预定物品被销毁）
            // 如果 JobGiver 成功预定，这里通常会成功。

            if (ReservationUtility.HasReserved(this.pawn, this.TargetItem))
            {
                return true; // 预定已存在，Job可以开始
            }
            return false;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // 确保 Job 的目标物品仍然有效且可达
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => !pawn.CanReach(TargetItem, PathEndMode.ClosestTouch, Danger.Some));
            
            // 假设 Pawn 的 Job 目标 A 是最终要购买的商品
            Thing finalTarget = TargetItem;

            // 1. **初始化浏览目标列表**
            // 目标列表应该包含 1-3 个 Pawn 附近的其他商品 + 最终目标 A
            // 步骤: a) 找到最终目标周围的其他商品 b) 将它们放入 TargetB 列表
            // 关键修正：获取附近物品的逻辑
            yield return Toils_General.Do(delegate
            {
                // 1. 获取所有 HaulableEver 物品 (广义上的可搬运物品)
                List<Thing> allHaulableItems =
                    pawn.Map.listerThings.ThingsMatching(ThingRequest.ForGroup(ThingRequestGroup.HaulableEver));

                // 2. 过滤并随机抽取周围的浏览目标
                Thing finalTarget = TargetItem;

                List<LocalTargetInfo> browseTargets = allHaulableItems
                    // 过滤：
                    .Where(t =>
                    {
                        // 距离检查：在 5 格范围内
                        if (!t.Position.InHorDistOf(pawn.Position, 5f)) return false;
                        // 排除最终目标 A
                        if (t == finalTarget) return false;
                        // 基础检查：不能是蓝图、尸体等，必须是可购买的商品 (使用 ShoppingUtility 的过滤逻辑的子集)
                        if (t.MarketValue <= 0f || t.IsForbidden(pawn) || t.def.tradeability == Tradeability.None)
                            return false;
                        // 可达性检查：必须能走到
                        if (!pawn.CanReach(t, PathEndMode.ClosestTouch, Danger.Some)) return false;
                        // 额外：排除 MinifiedThing
                        if (t is MinifiedThing) return false;
                        
                        return true;
                    })
                    // 随机抽取：使用 InRandomOrder() 和 Take(3) 替代 TakeRandom(3)
                    .InRandomOrder()
                    .Take(3)
                    // 转换为 LocalTargetInfo 列表
                    .Select(t => new LocalTargetInfo(t))
                    .ToList();

                // 3. 修复 Job.SetTargetQueue：手动添加目标
                // 确保 TargetQueueB 列表初始化/清空
                job.targetQueueB?.Clear();
                job.targetQueueB = new List<LocalTargetInfo>();

                foreach (LocalTargetInfo target in browseTargets)
                {
                    job.AddQueuedTarget(TargetIndex.B, target); // 使用 AddQueuedTarget 逐个添加
                }
                
            });

            // --- 2. 循环标签 ---
            Toil browseLoopStart = Toils_General.Label();
            yield return browseLoopStart;

            // 3. **从队列中取出下一个浏览目标**
            yield return Toils_JobTransforms.ExtractNextTargetFromQueue(TargetIndex.B, true);

            // 4. **前往当前浏览目标 B**
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch)
                .FailOnSomeonePhysicallyInteracting(TargetIndex.B);

            // 5. **浏览/停留**
            // 使用短等待，模拟停留和比较
            Toil browse = Toils_General.Wait(300) // 5秒停留
                .FailOnDestroyedOrNull(TargetIndex.B);

            browse.tickAction = () =>
            {
                // 旋转：确保 Pawn 随机环顾四周
                if (Find.TickManager.TicksGame % 60 == 0)
                {
                    this.pawn.Rotation = Rot4.Random;
                }

                // Joy：继续获得购物的 Joy 增益
                if (this.pawn.needs.joy != null)
                {
                    this.pawn.needs.joy.GainJoy(this.job.def.joyGainRate * 0.0001f, this.job.def.joyKind);
                }
            };
            yield return browse;

            // 7. **循环继续**
            // 如果 TargetB 队列中还有物品，跳回循环开始
            yield return Toils_Jump.JumpIf(browseLoopStart,
                () => job.targetQueueB != null && job.targetQueueB.Count > 0);

            
            // --- 8. 浏览结束，前往最终购买目标 A ---
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch)
                .FailOnSomeonePhysicallyInteracting(TargetIndex.A);

            // 9. **最终购买决策**
            // ⭐ FIX 1：禁止在 Toil 中直接 EndCurrentJob
            yield return Toils_General.Do(delegate
            {
                if (finalTarget == null || !ShouldBuyItem(pawn, finalTarget))
                {
                    // 什么都不做，让 Toil 自己结束
                }
            });

            // --- 10. 支付等待
            yield return Toils_General.Wait(TicksToPay)
                .FailOnDestroyedOrNull(TargetIndex.A)
                .WithProgressBarToilDelay(TargetIndex.B);

            // --- 11. 支付执行
            yield return Toil_VirtualPay_Instant();

            // --- 12. 拿取商品
            yield return Toil_TakeItem();
            
        }

        // --- Toil 辅助方法 ---
        // 核心 Toil: 虚拟支付 
        private Toil Toil_VirtualPay_Instant()
        {
            Toil toil = new Toil();
            toil.initAction = () =>
            {
                // 这里加上 InHorDistOf 检查，防止征召导致的提前支付
                if (!pawn.Position.InHorDistOf(TargetItem.Position, 2f))
                {
                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }

                var econ = pawn.GetEconomyData();

                if (econ == null)
                {
                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }
                
                //得到当前售价
                int totalPrice = Mathf.CeilToInt(TargetItem.MarketValue * TargetItem.stackCount);
                float preWallet = econ.virtualWallet;
                
                if (econ.virtualWallet < totalPrice)
                {
                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }

                if (!econ.SubtractMoney(totalPrice))
                {
                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }

                // ... (扣款、生成银子、Message) ...
                Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                silver.stackCount = totalPrice;
                GenSpawn.Spawn(silver, pawn.Position, pawn.Map);
                
                pawn.GetEconomyData().economicHistory.Add(EconomicLogEntry.NewLog($"花费{totalPrice}购买{TargetItem.LabelCap.Colorize(Color.cyan)}"));
                
                Messages.Message(
                    $"{pawn.LabelShort}购买了{TargetItem.LabelCap}：".Translate(pawn.NameShortColored, totalPrice, TargetItem.LabelCapNoCount), pawn,
                    MessageTypeDefOf.PositiveEvent, false);
            };

            // ⭐ FIX 3：Instant 完成，交给 Job 系统自然推进
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            toil.socialMode = RandomSocialMode.Off;
            return toil;
        }

        // 核心 Toil 2: 拿取商品 
        private Toil Toil_TakeItem()
        {
            Toil toil = new Toil();
            toil.initAction = () =>
            {
                Thing item = TargetItem;
                if (item == null || item.DestroyedOrNull())
                {
                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }

                // 1. 取消保留 (放在前面确保即使后续失败，预定也释放了)
                pawn.Map.reservationManager.Release(item, pawn, job);

                try
                {
                    // 确保我们操作的是一个独立的 Thing 实例，并且数量是整个堆叠
                    Thing itemToVirtualize = item.SplitOff(item.stackCount);

                    // 核心：调用新的虚拟化方法，保存数据并销毁物理物品
                    pawn.GetEconomyData().VirtualAndMarkAsset(itemToVirtualize, TargetItem.stackCount);
                    
                   
                    
                }
                catch (Exception e)
                {
                    
                }
            };

            // ⭐ FIX 4：绝不 EndCurrentJob，由 Toil 自然结束整个 Job
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            toil.socialMode = RandomSocialMode.Off;
            return toil;
        }
        
        // --- 额外的决策逻辑 ---
        private bool ShouldBuyItem(Pawn pawn, Thing item)
        {
            // (保持不变)
            float baseChance = 0.8f;

            var econ = pawn.GetEconomyData();
            float wallet = econ?.virtualWallet ?? 0f;

            if (wallet < 100f)
            {
                baseChance *= 0.5f;
            }

            float moodFactor = 1f;
            if (pawn.needs.mood.CurLevel < 0.4f)
            {
                moodFactor = 1.5f;
            }

            bool decision = Rand.Value < baseChance * moodFactor;
            

            return decision;
        }
    }
}