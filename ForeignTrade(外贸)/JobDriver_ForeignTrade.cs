using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using UnityEngine;

namespace EconomicSystem
{
    // 该 JobDriver 负责殖民者前往商队Pawn并与之进行虚拟交易(外贸)
    public class JobDriver_ForeignTrade : JobDriver
    {
        // TargetIndex.A 始终指向目标Pawn (TargetA)
        private const TargetIndex TargetTrader = TargetIndex.A;

        // 模拟交谈的持续时间（Ticks），例如 600 Ticks 约 10 秒
        private const int TicksToTrade = 600;

        // 用于获取商队 Pawn 的快捷方法
        private Pawn TraderPawn => (Pawn)this.job.GetTarget(TargetTrader).Thing;
        
        // 修正：从 job.dutyTag 中读取物品 DefName
        private string ItemDefName => this.job.dutyTag;


        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // 商队Pawn通常不需要或不能被保留，所以直接返回 true
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.AddFinishAction(OnJobFinished);
            
            // 失败条件: 如果商队Pawn死亡或离开地图
            this.FailOnDespawnedOrNull(TargetTrader);
            this.FailOnDowned(TargetTrader);

            // --- Toil 1: 前往商队Pawn（Goto） ---

            // 路径搜索模式为 Touch，意味着Pawn需要走到紧贴目标Pawn的位置
            Toil goToTrader = Toils_Goto.GotoThing(TargetTrader, PathEndMode.Touch)
                .FailOn(() => !TraderPawn.Spawned); // 目标Pawn必须在地图上

            yield return goToTrader;

            
            // ⭐ 新增 Toil 1.5：拿起虚拟物品并实例化 (Make Thing and Carry)
            yield return Toils_General.Do(delegate
            {
                
                
                // ⭐ 如果已经有视觉物品，就不要再生成
                Thing visualThing = job.GetTarget(TargetIndex.B).Thing;
                if (visualThing != null && pawn.carryTracker?.CarriedThing == visualThing)
                {
                    // Pawn 正在拿这个 Job 的视觉物品
                    return;
                }

                
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
                //（关键）设置该物体掉落及销毁
                sellingItem.def.destroyOnDrop = true;
                
                job.SetTarget(TargetIndex.B, sellingItem);
                
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
            // 使用 Wait 模拟交谈时间，并将 TargetTrader 设置为面向目标
            Toil interaction = Toils_General.Wait(TicksToTrade, TargetTrader);

            // 核心逻辑：持续检查距离并提供视觉反馈
            interaction.tickAction = delegate
            {
                // **追逐逻辑：如果商队Pawn走远，则跳回 Toil 1 (goToTrader)**
                // 如果Pawn离目标超过 3 格，则重新执行前往 Toil
                if (!pawn.Position.InHorDistOf(TraderPawn.Position, 4f))
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

                // 如果没有数据或无法找到待售物品，则失败
                if (data == null || ItemDefName.NullOrEmpty())
                {
                   // Log.Warning($"[CES] {pawn.NameShortColored} failed trade: Missing economy data or ItemDefName.");
                    this.EndJobWith(JobCondition.Errored);
                    return;
                }

                // 尝试执行交易逻辑
                ExecuteTradeAndSettle(pawn, data, ItemDefName);

                // 无论成功与否，Job 都视为完成
                this.EndJobWith(JobCondition.Succeeded);
            });

        }
        
        // 辅助方法：执行交易和结算
        private void ExecuteTradeAndSettle(Pawn pawn, CES_PawnEconomyData data, string defName)
        {
            // 1. 找到并移除待售的 PrivateItemData
            PrivateItemData itemData = data.privateOwnedAssets.FirstOrDefault(i => i.defName == defName);

            if (itemData == null)
            {
                //Log.Warning(
                    //$"[CES] {pawn.NameShortColored} failed trade: Item '{defName}' not found in private inventory.");
                // 如果物品找不到了，直接返回成功，避免无限 Job 失败
                return;
            }

            // 2. 模拟 RecreateThing 获取准确价值和数量
            Thing recreatedThing = itemData.RecreateThing();

            // 如果物品无法被重新创建（例如 Mod 卸载），则中止
            if (recreatedThing == null)
            {
                //Log.Error($"[CES] Failed to recreate item '{defName}' for trade settlement. Aborting.");
                data.privateOwnedAssets.Remove(itemData); // 移除损坏的数据
                return;
            }

            // 2.5. 随机判定交易是否成功 (例如 80% 成功率)
            // 简化：目前假设总是成功，但保留随机拒绝的扩展点
            if (Rand.Chance(0.30f)) // 20% 概率被商队拒绝购买
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

            // A. 殖民者移除虚拟物品,并在商队小人的背包中加入该物品
            data.privateOwnedAssets.Remove(itemData);
            TraderPawn.inventory.innerContainer.TryAdd(recreatedThing);

            // B. 增加虚拟货币
            data.AddMoney(netIncome);

            // C. 生成实体白银税收
            if (taxAmount > 0)
            {
                Thing silverTax = ThingMaker.MakeThing(ThingDefOf.Silver);
                silverTax.stackCount = taxAmount;

                // 生成在 Pawn 的位置
                GenSpawn.Spawn(silverTax, pawn.Position, pawn.Map);

                // 通知玩家税收已生成 (可选 Mote)
                MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, $"Tax: {taxAmount} Silver", Color.white,2.5f);
            }

            // D. 反馈 Mote (显示总收入)
            MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, $"Sold: +{netIncome}", Color.green, 3f);

            //Log.Message(
                //$"[CES] {pawn.NameShortColored} sold {itemData.defName} for {salePrice}. Net: {netIncome}, Tax: {taxAmount}.");

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