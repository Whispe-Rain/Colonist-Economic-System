using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using UnityEngine;

namespace EconomicSystem
{
    // 该 JobDriver 负责殖民者前往商队Pawn并与之进行虚拟交易(外贸)
    public class JobDriver_ForeignSelling : JobDriver
    {
        // TargetIndex.A 始终指向目标Pawn (TargetA)
        private const TargetIndex TargetTrader = TargetIndex.A;

        // 模拟交谈的持续时间（Ticks），例如 300 Ticks 约 5秒
        private const int TicksToTrade = 300;

        // 用于获取商队 Pawn 的快捷方法
        private Pawn TraderPawn => (Pawn)this.job.GetTarget(TargetTrader).Thing;

        // 修正：从 job.dutyTag 中读取物品 DefName
        private string ItemDefName => this.job.dutyTag;


        private bool tradeSucceeded;
        private string tradedDefName;

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
                CES_PawnEconomyData data = this.pawn.TryGetEconomyData();
                PrivateItemData itemData = data.privateOwnedAssets.FirstOrDefault(i => i.defName == ItemDefName);

                if (itemData == null)
                {
                    return;
                }

                // 3. 实例化 Thing (临时创建实体)
                Thing sellingItem = itemData.RecreateThing();

                if (sellingItem == null)
                {
                    // ★ 错误情况下，应该移除这个坏数据，避免 WorkGiver 循环选取
                    data.privateOwnedAssets.Remove(itemData);
                    return;
                }

                // 4. 关键：Pawn 拿起物品
                this.pawn.carryTracker.TryStartCarry(sellingItem);
                //（关键）设置该物体掉落及销毁
                sellingItem.def.destroyOnDrop = true;
                // 5. 将该物品设置为 TargetIndex.B，供后续 Toil 使用
                this.job.SetTarget(TargetIndex.B, sellingItem);
            });


            // --- Toil 2: 追逐/互动 Toil（Wait with Jump） ---
            // 使用 Wait 模拟交谈时间，并将 TargetTrader 设置为面向目标
            Toil interaction = Toils_General.Wait(TicksToTrade, TargetTrader)
                .WithProgressBarToilDelay(TargetIndex.A);

            // 核心逻辑：持续检查距离并提供视觉反馈
            interaction.tickAction = delegate
            {
                // **追逐逻辑：如果商队Pawn走远，则跳回 Toil 1 (goToTrader)**
                // 如果Pawn离目标超过 4 格，则重新执行前往 Toil
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
                CES_PawnEconomyData data = pawn.TryGetEconomyData();

                // 如果没有数据或无法找到待售物品，则失败
                if (data == null || ItemDefName.NullOrEmpty())
                {
                    return;
                }

                // 尝试执行交易逻辑
                ExecuteTradeAndSettle(pawn, data, ItemDefName);
            });
        }

        // 辅助方法：执行交易和结算
        private void ExecuteTradeAndSettle(Pawn pawn, CES_PawnEconomyData data, string defName)
        {
            // 1. 找到并移除待售的 PrivateItemData
            PrivateItemData itemData = data.privateOwnedAssets.FirstOrDefault(i => i.defName == defName);

            if (itemData == null)
            {
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

            // 2.5. 随机判定交易是否成功 
            //基础50%失败率，社交等级可以降低失败率，20级降低20%，及30%的失败率
            float fail = 0.5f - pawn.skills.GetSkill(SkillDefOf.Social).Level * 0.01f;
            if (Rand.Chance(fail)) //被商队拒绝购买
            {
                recreatedThing.Destroy(); // 销毁临时 Thing
                MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "贸易失败".Translate(), Color.red, 4f);
                
                //设置物品交易冷却CD，防止一直售卖同一个商品。
                int now = Find.TickManager.TicksGame;
                string cdKey = $"ForeignTradeFail:{itemData.defName}";
                data.coolDowns[cdKey] = now + Rand.Range(2500,7500); 
                
                return;
            }

            // 3. 计算价格
            float baseValue = (int)recreatedThing.MarketValue * itemData.stackCount;
            // 价格浮动：不可控的市场浮动（-0.5~0.5）+社交*0.01+智识*0.01+0.1
            //0.1f这10%是商业税，相当于先加价10%，然后卖家全额负担商品税10%
            float priceFactor = itemData.priceChange + 0.1f +
                                pawn.skills.GetSkill(SkillDefOf.Social).Level * 0.01f +
                                pawn.skills.GetSkill(SkillDefOf.Intellectual).Level * 0.01f;

            int salePrice = Mathf.CeilToInt(baseValue + baseValue * priceFactor);

            // 4. 计算税收 (10% 商业税)
            int taxAmount = Mathf.CeilToInt(salePrice * 0.10f);
            int netIncome = salePrice - taxAmount;

            // 5. 执行结算操作

            // A. 殖民者移除虚拟物品,并在商队小人的背包中加入该物品
            data.RemoveAsset(itemData, netIncome);


            //随机判定：以物易物 (Barter) 还是白银交易 (Silver)
            bool isBarterTrade = Rand.Chance(0.40f); // 40% 概率以物易物
            if (isBarterTrade)
            {
                // 1. 随机选择一个可注入的物品定义
                // 我们选择一个最大价值略高于目标价值的物品，防止只选到低价值的。
                ThingDef defToInject = ForeignUtility.GetRandomInThingsDef(salePrice * 2).RandomElement();
                
                //为该物品选择材质，如果需要的话。
                ThingDef stuff = ForeignUtility.ChooseSafeRandomStuff(defToInject);
                
                Thing ToInject = ThingMaker.MakeThing(defToInject,stuff);
                if (defToInject != null)
                {
                    //得到该物品的市场价
                    float itemMarketValue = defToInject.BaseMarketValue;

                    if (itemMarketValue > 0)
                    {
                        // 2. 计算可以兑换的数量
                        // 数量 = floor( 目标价值 / 单个物品市场价值 )
                        int count = Mathf.FloorToInt(salePrice / itemMarketValue);
                        if (count > 0)
                        {
                            // 3. 计算实际兑换的价值
                            int actualValue = Mathf.RoundToInt(count * itemMarketValue);

                            // 4. 注入到 Pawn 的私有背包数据中
                            data.VirtualAndMarkAsset(ToInject, count);
                            // data.AddAsset(defToInject, count);

                            // 5. 将兑换后的剩余价值（找零）以白银形式注入 (可选，用于精确匹配)
                            int remainder = salePrice - actualValue;
                            if (remainder > 0)
                            {
                                data.AddMoney(remainder);
                            }

                            // 6. 反馈 Mote 和日志
                            string feedbackText = $"以物易物: +{defToInject.label} x{count}";
                            if (remainder > 0)
                            {
                                feedbackText += $" (+{remainder} 白银)";
                            }

                            MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, feedbackText, Color.cyan, 3.5f);

                            string history =
                                $"与{TraderPawn.NameShortColored}以物易物，换取了{defToInject.label.Colorize(Color.cyan)} x{count}，找零{remainder}白银";
                            data.AddHistory(data.economicHistory, history);

                            
                            //设置冷却CD，防止刚到手就卖给商队。
                            int now = Find.TickManager.TicksGame;
                            string cdKey = $"ForeignTrade:{defToInject.defName}";
                            data.coolDowns[cdKey] = now + 60000; // 60000 ticks = 1天
                
                            //设置冷却CD，防止刚到手就卖掉其他小人（其他小人又反向卖给商队）。
                            string cdKey2 = $"Trade:{defToInject.defName}";
                            data.coolDowns[cdKey2] = now + 60000; // 60000 ticks = 1天
                        }
                        else
                        {
                            // 找到了物品，但目标价值太低，连一个都换不起。
                            // 此时默认转为白银交易，或者直接将 netIncome 注入白银。
                            data.AddMoney(salePrice);
                            MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "交易失败 - 转换为银币".Translate(),
                                Color.yellow, 3f);

                            string history = $"与{TraderPawn.NameShortColored}以物易物失败，转为白银交易+{salePrice}。";
                            data.AddHistory(data.economicHistory, history);
                        }
                    }
                }
            }
            else
            {
                
                
                //计算利润并记录
                int profit = data.GetProfit(itemData.sellPrice, itemData.buyPrice);
                data.Profit += profit;
                if (profit > 0)
                {
                    //盈利售卖日志
                    string history = $"将{itemData.Name.Colorize(Color.yellow)}出售给{TraderPawn.NameShortColored}" +
                                     $",净利润{profit.ToString().Colorize(Color.green)}";
                    data.AddHistory(data.economicHistory, history);
                }
                else
                {
                    //亏损售卖日志
                    string history = $"将{itemData.Name.Colorize(Color.yellow)}出售给{TraderPawn.NameShortColored}" +
                                     $",净利润{profit.ToString().Colorize(Color.red)}";
                    data.AddHistory(data.economicHistory, history);
                }

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
                    MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, $"交易税: {taxAmount} 白银", Color.white, 4f);
                }

                // D. 反馈 Mote (显示总收入)
                MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, $"售出价: +{netIncome}", Color.green, 4f);

                // 销毁临时 Thing (必须在所有计算完成后进行)
                recreatedThing.Destroy();
            }
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