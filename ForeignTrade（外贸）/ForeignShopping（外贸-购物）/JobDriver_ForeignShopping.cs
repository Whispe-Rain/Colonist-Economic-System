using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace EconomicSystem
{
    public class JobDriver_ForeignShopping : JobDriver
    {
        // TargetIndex.A 始终指向目标Pawn (TargetA)
        private const TargetIndex TargetTrader = TargetIndex.A;
        private Pawn TraderPawn => (Pawn)this.job.GetTarget(TargetTrader).Thing;

        // 模拟交谈的持续时间（Ticks），例如 300 Ticks 约 5秒
        private const int TicksToTrade = 300;


        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // 商队Pawn通常不需要或不能被保留，所以直接返回 true
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // 失败条件: 如果商队Pawn死亡或离开地图
            this.FailOnDespawnedOrNull(TargetTrader);
            this.FailOnDowned(TargetTrader);

            // --- Toil 1: 前往商队Pawn（Goto） ---

            // 路径搜索模式为 Touch，意味着Pawn需要走到紧贴目标Pawn的位置
            Toil goToTrader = Toils_Goto.GotoThing(TargetTrader, PathEndMode.Touch)
                .FailOn(() => !TraderPawn.Spawned); // 目标Pawn必须在地图上

            yield return goToTrader;


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

                //随机本次购买预算
                int budget = Rand.Range(50, 400);


                // 如果没有数据或无法找到待售物品，则失败
                if (data == null)
                {
                    return;
                }
                
                // 尝试执行交易逻辑
                ExecuteTradeAndSettle(pawn, data, budget);
            });
        }

        public void ExecuteTradeAndSettle(Pawn pawn, CES_PawnEconomyData data, int budget)
        {
            // 随机判定交易是否成功,40%拒绝购买 
            float fail = 0.4f;
            if (Rand.Chance(fail)) //小人拒绝购买
            {
                MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "购物失败".Translate(), Color.red, 4f);
                return;
            }
            
            // 1. 随机选择一个可注入的物品定义
            // 我们选择一个最大价值略高于目标价值的物品，防止只选到低价值的。
            ThingDef defToInject = ForeignUtility.GetRandomInThingsDef(budget * 2).RandomElement(); 
            
            //为该物品选择材质，如果需要的话。
            ThingDef stuff = ForeignUtility.ChooseSafeRandomStuff(defToInject);
            
            Thing ToInject = ThingMaker.MakeThing(defToInject,stuff);
            if (defToInject != null)
            {
                //得到该物品的市场价
                float itemMarketValue = defToInject.BaseMarketValue;

                // 2. 计算可以兑换的数量
                // 数量 = floor( 目标价值 / 单个物品市场价值 )
                int count = Mathf.FloorToInt(budget / itemMarketValue);
                if (count <= 0)
                {
                    //一个都买不起,直接结束。
                    return;
                }

                // 3. 计算总价格
                int actualValue = Mathf.RoundToInt(count * itemMarketValue);

                // 4. 注入到 Pawn 的私有背包数据中
                data.VirtualAndMarkAsset(ToInject, count);

                // 5. 将兑换后的剩余价值（找零）以白银形式注入
                int remainder = budget - actualValue;
                if (remainder > 0)
                {
                    data.AddMoney(remainder);
                }
                
                //扣除小人的钱
                data.virtualWallet-=actualValue;
                
                // 6. 反馈 Mote 和日志
                string feedbackText = $"从商队处购买了: +{defToInject.label} x{count}";
                if (remainder > 0)
                {
                    feedbackText += $" (-{remainder} 白银)";
                }

                MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, feedbackText, Color.cyan, 3.5f);

                string history =
                    $"与{TraderPawn.NameShortColored}进行贸易，购买了{defToInject.label.Colorize(Color.cyan)} x{count}，找零{remainder}白银";
                data.AddHistory(data.economicHistory, history);

                //设置冷却CD，防止刚到手就卖给商队。
                int now = Find.TickManager.TicksGame;
                string cdKey = $"ForeignTrade:{defToInject.defName}";
                data.coolDowns[cdKey] = now + 60000; // 60000 ticks = 1天
                
                //设置冷却CD，防止刚到手就卖掉其他小人（其他小人又反向卖给商队）。
                string cdKey2 = $"Trade:{defToInject.defName}";
                data.coolDowns[cdKey2] = now + 60000; // 60000 ticks = 1天
                
                //购物CD（游戏中1小时~3小时）浮动
                string cdKey3 = $"ShoppingFail:{TraderPawn.Name}";
                data.coolDowns[cdKey3] = now + Rand.Range(2500, 7500); // 1~3个小时
            }
        }
    }
}