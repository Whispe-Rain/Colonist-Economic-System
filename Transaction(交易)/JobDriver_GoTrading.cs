using System.Collections.Generic;
using Hospitality.Utilities;
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
                //Log.Warning($"[CES_DEBUG] JobDriver denied: Pawn {pawn.NameShortColored} has no Economy Data.");
                return false;
            }

            //⭐ 核心修正：直接尝试预定目标 Pawn
            // 预定参数：目标Pawn，当前Job，预定数量(1)，堆叠(-1)，错误标志(errorOnFailed)
            bool reserved = this.pawn.Reserve(targetPawn, this.job, 1, -1, null, errorOnFailed);

            return reserved;
        }
        
        protected override IEnumerable<Toil> MakeNewToils()
        {
            //##核心代码,适合用来处理任何Job中断后的处理（优先级很高）##   
            this.AddFinishAction(OnJobFinished);
            
            // 失败条件: 如果Pawn死亡或离开地图
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
                // ⭐ 如果已经有视觉物品，就不要再生成
                if (job.GetTarget(TargetIndex.B).Thing != null)
                    return;
                
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
            // 使用 Wait 模拟交谈时间，并将 目标 设置为面向目标
            Toil interaction = Toils_General.Wait(TicksToChat, TargetIndex.A)
                .WithProgressBarToilDelay(TargetIndex.A);;

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
                if (!pawn.Position.InHorDistOf(TargetPawn.Position, 5f))
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
                    //Log.Warning($"[CES] {pawn.NameShortColored} failed trade: Missing economy data or ItemDefName.");
                    this.EndJobWith(JobCondition.Errored);
                    return;
                }

                // 尝试执行交易逻辑
                ExecuteTradeAndSettle(pawn, TargetPawn, data, ItemDefName);
                
                // 无论成功与否，Job 都视为完成
                this.EndJobWith(JobCondition.Succeeded);
            });


        }


        // 辅助方法：执行交易和结算
        private void ExecuteTradeAndSettle(Pawn pawn, Pawn targetPawn, CES_PawnEconomyData data, string defName)
        {
            
            // 获取 Toil 1.5 中拿起的物品
            Thing carriedThing = (Thing)job.GetTarget(TargetIndex.B).Thing; // ⭐ 确保拿到正确的实例
            
            // 1. 找到并移除待售的 PrivateItemData
            PrivateItemData itemData = data.privateOwnedAssets.FirstOrDefault(i => i.defName == defName);


            if (itemData == null)
            {
                // 如果物品找不到了，直接返回成功，避免无限 Job 失败
                pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                return;
            }

            // 2. 模拟 RecreateThing 获取准确价值和数量
            Thing recreatedThing = itemData.RecreateThing();

            // 如果物品无法被重新创建（例如 Mod 卸载），则中止
            if (recreatedThing == null)
            {
                //Log.Error($"[CES] 用于交易的物品重建‘{defName}’失败");
                return;
            }

            // 2.5. 随机判定交易是否成功
            if (Rand.Chance(0.30f)) // 30% 概率被殖民者拒绝购买
            {
                //recreatedThing.Destroy(); // 销毁临时 Thing
                MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "交易失败".Translate(), Color.red, 4f);
                
                return ;
            }

            if (itemData.buyPrice==0)
            {
                itemData.buyPrice = (int)recreatedThing.MarketValue* itemData.stackCount;
            }

            // 3. 计算价格
            float baseValue = (int)recreatedThing.MarketValue* itemData.stackCount;
            // 价格浮动：不可控的市场浮动（-0.5~0.5）+社交*0.01+智识*0.01+0.1
            //0.1f这10%是商业税，相当于先加价10%，然后卖家全额负担商品税10%
            float priceFactor =itemData.priceChange+0.1f+
                               pawn.skills.GetSkill(SkillDefOf.Social).Level*0.01f+
                               pawn.skills.GetSkill(SkillDefOf.Intellectual).Level*0.01f;
            
            int salePrice = Mathf.CeilToInt(baseValue+baseValue * priceFactor);

            // 4. 计算税收 (10% 商业税)
            int taxAmount = Mathf.CeilToInt(salePrice * 0.10f);
            //卖家到手的钱
            int netIncome = salePrice - taxAmount;
            
            // 5. 执行结算操作
            // A. 移除虚拟物品
            data.RemoveAsset(itemData,netIncome);
            
            //计算利润并记录
            int profit= data.GetProfit(itemData.sellPrice,itemData.buyPrice);
            data.Profit+=profit;
            if (profit>0)
            {
                //盈利售卖日志
                string history = $"将{itemData.Name.Colorize(Color.yellow)}卖给{targetPawn.NameShortColored}" +
                                 $",赚取+{profit.ToString().Colorize(Color.green)}";
                data.AddHistory(pawn.GetEconomyData().economicHistory,history);
                
               
            }
            else
            {
                //亏损售卖日志
                string history2 =  $"将{itemData.Name.Colorize(Color.yellow)}麦给{targetPawn.NameShortColored}" +
                                   $",亏损{profit.ToString().Colorize(Color.red)}";
                data.AddHistory(pawn.GetEconomyData().economicHistory,history2);
            }
            
            //买家添加物品并付钱给卖家
            if (targetPawn.GetEconomyData().AddAsset(itemData,salePrice))//相当于货到付款
            {
               
                //生成购买日志
                string history3 =  $"从{pawn.NameShortColored}花费{salePrice}白银买{itemData.Name.Colorize(Color.yellow)}";
                targetPawn.GetEconomyData().AddHistory(targetPawn.GetEconomyData().economicHistory,history3);
                
                // B. 增加虚拟货币
                targetPawn.GetEconomyData().virtualWallet -= salePrice;
                data.AddMoney(netIncome);
               
            }
            //为成功交易的物品设置冷却CD，防止刚到手就卖掉。
            string cdKey = $"Trade:{itemData.defName}";
            int now = Find.TickManager.TicksGame;

            data.coolDowns[cdKey] = now + 60000; 

            // C. 生成实体白银税收
            if (taxAmount > 0)
            {
                Thing silverTax = ThingMaker.MakeThing(ThingDefOf.Silver);
                silverTax.stackCount = taxAmount;

                // 生成在 Pawn 的位置
                GenSpawn.Spawn(silverTax, pawn.Position, pawn.Map);

                // 通知玩家税收已生成 (可选 Mote)
                MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, $"商品税: {taxAmount} 白银", Color.white, 4f);
            }

            // D. 反馈 Mote (显示总收入)
            MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, $"售出价: +{netIncome}", Color.green, 4f);
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