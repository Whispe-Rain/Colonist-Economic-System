using System;
using Verse;
using Verse.AI;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib; 
using System.Linq;

namespace EconomicSystem
{
    public class JobDriver_GoShopping : JobDriver
    {
        // Toil 的 Job 持续时间（Ticks）
        private const int TicksToBrowse = 900; 
        private const int TicksToPay = 90;      

        private Thing TargetItem => TargetA.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            var econ = pawn.GetEconomyData();
            if (econ == null)
            {
                Log.Warning($"[CES_DEBUG] JobDriver denied: Pawn {pawn.NameShortColored} has no Economy Data.");
                return false;
            }
            // 检查预定是否仍然有效（主要是防止预定期间物品被销毁）
            // 如果 JobGiver 成功预定，这里通常会成功。
    
            if (ReservationUtility.HasReserved(this.pawn, this.TargetItem)) 
            {
                Log.Message($"[CES_DEBUG] JobDriver Start: {pawn.NameShortColored} reservation for {TargetItem.LabelCap} confirmed.");
                return true; // 预定已存在，Job可以开始
            }
    
            // 如果因某些原因预定失效，则重新尝试预定（虽然不推荐，但作为安全回退）
            bool reserved = this.pawn.Reserve(TargetItem, this.job, 1, -1, null, errorOnFailed);
    
            if (!reserved)
            {
                Log.Message($"[CES_DEBUG] JobDriver denied: Could not reserve {TargetItem.LabelCap} as a fallback.");
            }
            else
            {
                Log.Message($"[CES_DEBUG] JobDriver Start: {pawn.NameShortColored} reserved {TargetItem.LabelCap} via fallback. Wallet: {econ.virtualWallet:F0}.");
            }

            return reserved;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // 确保 Job 的目标物品仍然有效且可达
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => !pawn.CanReach(TargetItem, PathEndMode.ClosestTouch, Danger.Some));

            // --- 0. 定义最终结束 Toil (用于跳转) ---
            // 关键修正：确保 endToil 是一个完整的 Toil，并让它立即结束 Job
            Toil endToil = Toils_General.Do(delegate
            {
                Log.Message($"[CES_DEBUG] Job Complete: {pawn.NameShortColored} ending JobDriver_GoShopping.");
                // 使用 JumpToToil 也会调用 Cleanup，所以最好使用 EndJobWith
                this.EndJobWith(JobCondition.Succeeded); 
            });
            // 确保这个 Toil 只是一个执行动作的容器
            endToil.defaultCompleteMode = ToilCompleteMode.Instant;
            
            // --- 1. 前往目标物品 ---
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch)
                .FailOnSomeonePhysicallyInteracting(TargetIndex.A);

            // --- 2. 浏览商品中 (可能决定不买) ---
            Toil browse = Toils_General.Wait(TicksToBrowse)
                .FailOnDestroyedOrNull(TargetIndex.A);
            
            browse.tickAction = () =>
            {
                // 随机地让小人旋转，模拟环顾四周
                if (Find.TickManager.TicksGame % 60 == 0) // 每隔60 Tick (~1秒) 尝试旋转一次
                {
                    // 随机选择一个方向，或者面向某个方向停留
                    Rot4 randomRot = Rot4.Random;
                    this.pawn.Rotation = randomRot;
                }
                // 此外，可以添加心情或 Joy 值的 Tick 效果，使其在浏览过程中感觉更快乐
                if (this.pawn.needs.joy != null)
                {
                    // 给予微小的 Joy 提升，模拟购物带来的愉悦感
                    this.pawn.needs.joy.GainJoy(this.job.def.joyGainRate * 0.0001f,JoyKindDefOf.Social);
                }
            };

            browse.AddFinishAction(() =>
            {
                // 浏览结束后，决定是否购买
                Thing item = TargetItem;
                if (item == null || !ShouldBuyItem(this.pawn, item)) 
                {
                    string itemLabel = item?.LabelCap ?? "Target Item (Null)";
                    Log.Message($"[CES_DEBUG] Decision Fail: {pawn.NameShortColored} skipped buying {itemLabel}. Jumping to end Toil.");
            
                    // ⭐ 核心修复点：不使用 JumpToToil，而是直接 EndJobWith
                    // JumpToToil 可能会在非预期的 Cleanup 阶段触发 NRE。
                    this.EndJobWith(JobCondition.Succeeded); 
                }
                else
                {
                    Log.Message($"[CES_DEBUG] Decision Success: {pawn.NameShortColored} will proceed to pay for {item.LabelCap}.");
                }
            });
            yield return browse;
            
            // --- 2a. 等待支付 (显示进度条) ---
            // 这个 Toil 现在是 Index 2
            yield return Toils_General.Wait(TicksToPay) // TicksToPay = 60 ticks
                .FailOnDestroyedOrNull(TargetIndex.A)
                .WithProgressBarToilDelay(TargetIndex.A); 
                // ⭐ 修复点：替换为 Research 效果
            
            
            // --- 2b. 支付环节 (执行扣款和生成银子) ---
            // 这个 Toil 现在是 Index 3
            yield return Toil_VirtualPay_Instant(); // 使用新的 Toil 名称

            // --- 4. 拿取商品 (Toil Index 3) ---
            // ⭐ 结合上次的修复：Toil_TakeItem 现在应该在内部调用 this.EndJobWith
            yield return Toil_TakeItem();
            
        }

        // --- Toil 辅助方法 ---

        // 核心 Toil 1: 虚拟支付 (保持不变)
        private Toil Toil_VirtualPay()
        {
            Toil toil = new Toil();
            toil.initAction = () =>
            {
                var econ = pawn.GetEconomyData();
                if (econ == null)
                {
                    Log.Warning($"[CES_DEBUG] Pay Toil Denied: Pawn has no Economy Data.");
                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }

                int totalPrice = Mathf.CeilToInt(TargetItem.MarketValue * TargetItem.stackCount); 
                float preWallet = econ.virtualWallet;

                Log.Message($"[CES_DEBUG] Pay Check: {pawn.NameShortColored} is paying {totalPrice}. Current Wallet: {preWallet:F0}.");
                
                if (econ.virtualWallet < totalPrice)
                {
                    Log.Error($"[CES_DEBUG] Pay FAIL: Insufficient funds. Need {totalPrice}, Have {preWallet}. Job Incompletable.");
                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }
                
                if (!econ.SubtractMoney(totalPrice))
                {
                    Log.Error($"[CES_DEBUG] Pay FAIL: SubtractMoney returned false for {pawn.NameShortColored}. Job Incompletable.");
                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }
                
                Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                silver.stackCount = totalPrice;
                GenSpawn.Spawn(silver, pawn.Position, pawn.Map);
                
                Messages.Message("ColonistBoughtItem".Translate(pawn.NameShortColored, totalPrice, TargetItem.LabelCapNoCount), pawn, MessageTypeDefOf.PositiveEvent, false);
                
                Log.Message($"[CES_DEBUG] Pay SUCCESS: {pawn.NameShortColored} paid {totalPrice}. New Wallet: {econ.virtualWallet:F0}.");
            };
            
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            toil.socialMode = RandomSocialMode.Off;
            return toil;
        }

        // 核心 Toil 2: 拿取商品 (⭐ 架构修正：改为虚拟化)
        private Toil Toil_TakeItem()
        {
            Toil toil = new Toil();
            toil.initAction = () =>
            {
                Thing item = TargetItem;
                if (item == null || item.DestroyedOrNull())
                {
                    // 如果物品已失效，直接成功结束 Job
                    this.EndJobWith(JobCondition.Succeeded); 
                    return;
                }
        
                // 1. 取消保留 (放在前面确保即使后续失败，预定也释放了)
                pawn.Map.reservationManager.Release(item, pawn, job);

                try
                {
                    // 确保我们操作的是一个独立的 Thing 实例，并且数量是整个堆叠
                    Thing itemToVirtualize = item.SplitOff(item.stackCount); 

                    // 核心：调用新的虚拟化方法，保存数据并销毁物理物品
                    pawn.GetEconomyData().VirtualAndMarkAsset(itemToVirtualize);

                    // 日志 9: 拿取成功 (虚拟化)
                    Log.Message($"[CES_DEBUG] Take Item SUCCESS: {pawn.NameShortColored} successfully virtualized {itemToVirtualize.LabelCap} as private asset.");
            
                    // ⭐ 关键修改：直接在这里结束 Job，不进入下一个 Toil
                    this.EndJobWith(JobCondition.Succeeded); 

                }
                catch (Exception e)
                {
                    Log.Error($"[CES_DEBUG] Toil_TakeItem failed for {pawn.NameShortColored}: {e.Message}");
                    this.EndJobWith(JobCondition.Errored);
                }
            };
    
            // **移除此行或改为 None，确保 initAction 成功后直接 EndJobWith**
            toil.defaultCompleteMode = ToilCompleteMode.Never; // 或者 Instant 保持不变，但依赖上面的 EndJobWith
            toil.socialMode = RandomSocialMode.Off;
            return toil;
        }
        // 核心 Toil: 虚拟支付 (只包含 Instant 逻辑)
// 更改方法名称以避免混淆
        private Toil Toil_VirtualPay_Instant() 
        {
            Toil toil = new Toil();
            toil.initAction = () =>
            {
                // ⭐ 记得在这里加上 InHorDistOf 检查，防止征召导致的提前支付
                if (!pawn.Position.InHorDistOf(TargetItem.Position, 2f)) 
                {
                    Log.Warning($"[CES_DEBUG] Pay Toil Denied: Pawn is too far. Re-queuing job.");
                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }

                var econ = pawn.GetEconomyData();
                // ... (原 Toil_VirtualPay 的所有支付逻辑) ...
        
                int totalPrice = Mathf.CeilToInt(TargetItem.MarketValue * TargetItem.stackCount); 
                // ... (扣款、生成银子、Message) ...
        
                Log.Message($"[CES_DEBUG] Pay SUCCESS: {pawn.NameShortColored} paid {totalPrice}. New Wallet: {econ.virtualWallet:F0}.");
            };
    
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

            Log.Message($"[CES_DEBUG] Buy Decision Calc: Base={baseChance:P0}, MoodFactor={moodFactor:F2}, FinalChance={baseChance * moodFactor:P2}. Result: {decision}.");

            return decision;
        }
    }
}