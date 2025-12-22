using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace EconomicSystem
{
    public class JobDriver_GoTrading : JobDriver
    {
        // Toil 的 Job 持续时间（Ticks）
        private const int TicksToChat = 300;

        // 用于获取目标Pawn 的快捷方法
        private Pawn TargetPawn => (Pawn)job.GetTarget(TargetIndex.A).Thing;

        //得到dutyTag(这里是待售商品的名字)
        private string ItemDefName => job.dutyTag;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            LocalTargetInfo targetPawn = job.GetTarget(TargetIndex.A);

            var econ = pawn.GetEconomyData();
            if (econ == null)
            {
                Log.Warning($"[CES_DEBUG] JobDriver denied: Pawn {pawn.NameShortColored} has no Economy Data.");
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
                // 如果预定失败，说明目标在 JobGiver 检查后到 JobDriver 启动前被别人抢走了
                Log.Warning(
                    $"[CES_DEBUG] JobDriver denied: {pawn.NameShortColored} 无法预定 {TargetPawn.NameShortColored}。");
            }

            return reserved;
        }
        
        protected override IEnumerable<Toil> MakeNewToils()
        {
            //##核心代码,适合用来处理任何Job中断后的处理（优先级很高）##   
            this.AddFinishAction(OnJobFinished);
            
            // 失败条件: 如果商队Pawn死亡或离开地图
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOnDowned(TargetIndex.A);
            this.FailOn(() => this.pawn.Drafted);

            // --- Toil 1: 前往目标客户（Goto） ---
            // 路径搜索模式为 Touch，意味着Pawn需要走到紧贴目标Pawn的位置
            Toil goToTrader = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch)
                .FailOn(() => !TargetPawn.Spawned); // 目标Pawn必须在地图上

            yield return goToTrader;


            // ⭐ 新增 Toil 1.5：拿起虚拟物品并实例化 (Make Thing and Carry)
            yield return Toils_General.Do(delegate
            {
                // 2. 找到并从 Pawn 的私人资产中移除（防止交易中途卖掉）
                CES_PawnEconomyData data = this.pawn.GetEconomyData();
                PrivateItemData itemData = data.privateOwnedAssets.FirstOrDefault(i => i.defName == ItemDefName);

                if (itemData == null)
                {
                    this.EndJobWith(JobCondition.Errored);
                    return;
                }

                // 3. 实例化 Thing (临时创建实体)
                Thing sellingItem = itemData.RecreateThing();
                if (sellingItem == null)
                {
                    this.EndJobWith(JobCondition.Errored);
                    return;
                }

                // 4. 关键：Pawn 拿起物品
                this.pawn.carryTracker.TryStartCarry(sellingItem);

                // 5. 将该物品设置为 TargetIndex.B，供后续 Toil 使用
                this.job.SetTarget(TargetIndex.B, sellingItem);
            });



            // --- Toil 2: 追逐/互动 Toil（Wait with Jump） ---
            // 使用 Wait 模拟交谈时间，并将 目标 设置为面向目标
            Toil interaction = Toils_General.Wait(TicksToChat, TargetIndex.A);

            // 核心逻辑：持续检查距离并提供视觉反馈
            interaction.tickAction = delegate
            {
                
                // Joy：继续获得交易Joy 增益
                if (pawn.needs.joy != null)
                {
                    this.pawn.needs.joy.GainJoy(this.job.def.joyGainRate * 0.0001f, this.job.def.joyKind);
                }

                // **追逐逻辑：如果Pawn走远，则跳回 Toil 1 (goToTrader)**
                // 如果Pawn超过 4 格，则重新执行前往 Toil
                if (!pawn.Position.InHorDistOf(TargetPawn.Position, 4f))
                {
                    // 重置计时器，并跳回 Toil 1 (前往)
                    pawn.jobs.curDriver.JumpToToil(goToTrader);
                }
            };

            interaction.defaultCompleteMode = ToilCompleteMode.Delay; // 计时器结束后完成
            yield return interaction;

            // --- Toil 3: 交易结算 Toil（Do） ---
            yield return Toils_General.Do(delegate
            {
                // 获取Pawn的经济数据
                CES_PawnEconomyData data = pawn.GetEconomyData();

                // 如果自身和买方任意一个经济数据不存在，直接结束任务
                if (data == null || TargetPawn.GetEconomyData() == null)
                {
                    Log.Warning($"[CES] {pawn.NameShortColored} failed trade: Missing economy data or ItemDefName.");
                    this.EndJobWith(JobCondition.Errored);
                    return;
                }

                // 尝试执行交易逻辑
                ExecuteTradeAndSettle(pawn, TargetPawn, data, ItemDefName);
                // 无论成功与否，Job 都视为完成
                this.EndJobWith(JobCondition.Succeeded);
            });
            
            this.AddEndCondition(delegate
            {
                if (this.pawn.Dead || this.pawn.Downed || this.pawn.Drafted || this.pawn.InMentalState)
                {
                    // ❗ 只留失败判断。清理由 Harmony 负责。
                    // 如果 Harmony 没有触发，说明 Job 失败不是由丢弃引起，但物品仍需清理。
                    // 我们可以信任 JobDriver 在结束时清理 Target B。
        
                    // 如果您不放心，可以在这里添加一个安全检查：
                    // if(this.job.GetTarget(TargetIndex.B).Thing != null) { this.DropAndDestroyCarriedThing(this.pawn); }
        
                    return JobCondition.Incompletable;
                }
                return JobCondition.Ongoing; 
            });


        }


        // 辅助方法：执行交易和结算
        private void ExecuteTradeAndSettle(Pawn pawn, Pawn targetPawn, CES_PawnEconomyData data, string defName)
        {
            // 1. 找到并移除待售的 PrivateItemData
            PrivateItemData itemData = data.privateOwnedAssets.FirstOrDefault(i => i.defName == defName);


            if (itemData == null)
            {
                Log.Warning(
                    $"[CES] {pawn.NameShortColored} failed trade: 物品 '{defName}' 没有在私人背包中找到.");
                // 如果物品找不到了，直接返回成功，避免无限 Job 失败
                return;
            }

            // 2. 模拟 RecreateThing 获取准确价值和数量
            Thing recreatedThing = itemData.RecreateThing();

            // 如果物品无法被重新创建（例如 Mod 卸载），则中止
            if (recreatedThing == null)
            {
                Log.Error($"[CES] 用于交易的物品重建‘{defName}’失败");
                data.privateOwnedAssets.Remove(itemData); // 移除损坏的数据
                return;
            }

            // 2.5. 随机判定交易是否成功
            if (Rand.Chance(0.30f)) // 30% 概率被殖民者拒绝购买
            {
                recreatedThing.Destroy(); // 销毁临时 Thing
                MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "TradeFailed".Translate(), Color.red, 3f);
                return;
            }

            // 3. 计算价格
            float baseValue = recreatedThing.MarketValue * itemData.stackCount;
            // 价格浮动：模拟砍价和加价 (85% 到 115%)
            float priceFactor = Rand.Range(0.75f, 1.15f);
            int salePrice = Mathf.CeilToInt(baseValue * priceFactor);

            // 4. 计算税收 (10% 商业税)
            int taxAmount = Mathf.CeilToInt(salePrice * 0.10f);
            int netIncome = salePrice - taxAmount;

            // 5. 执行结算操作
            // A. 移除虚拟物品
            data.RemoveAsset(itemData);

            //为买家添加物品
            if (targetPawn.GetEconomyData().AddAsset(itemData))
            {
                // B. 增加虚拟货币
                data.AddMoney(netIncome);
            }

            // C. 生成实体白银税收
            if (taxAmount > 0)
            {
                Thing silverTax = ThingMaker.MakeThing(ThingDefOf.Silver);
                silverTax.stackCount = taxAmount;

                // 生成在 Pawn 的位置
                GenSpawn.Spawn(silverTax, pawn.Position, pawn.Map);

                // 通知玩家税收已生成 (可选 Mote)
                MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, $"Tax: {taxAmount} Silver", Color.white, 2.5f);
            }

            // D. 反馈 Mote (显示总收入)
            MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, $"Sold: +{netIncome}", Color.green, 3f);

            Log.Message(
                $"[CES] {pawn.NameShortColored} sold {itemData.defName} for {salePrice}. Net: {netIncome}, Tax: {taxAmount}.");

            // 销毁临时 Thing (必须在所有计算完成后进行)
            recreatedThing.Destroy();
        }
        
        private void OnJobFinished(JobCondition condition)
        {
            var carried = pawn.carryTracker?.CarriedThing;
            if (carried == null) return;

            // 只销毁你这个 Job 生成的“视觉物品”
            if (job.GetTarget(TargetIndex.B).Thing == carried)
            {
                // ★ 关键：先从 innerContainer 移除
                pawn.carryTracker.innerContainer.Remove(carried);

                // ★ 再销毁，且不能 Drop
                carried.Destroy(DestroyMode.Vanish);
            }
        }
    }
}