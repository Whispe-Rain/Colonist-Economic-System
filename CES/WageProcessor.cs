using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace EconomicSystem
{
    /// <summary>
    /// 工资结算器
    /// 
    /// 职责：
    /// - 每天结算一次
    /// - 将 Pawn 的工作价值转化为工资
    /// - 尝试从殖民地账户支付
    /// </summary>
    public class WageProcessor : MapComponent
    {
        // 一天的 Tick 数（RimWorld 常量）
        private const int TicksPerDay = 60000;

        //三天缴纳一次个人所得税
        private const int TickTaxDay = 180000; 

        // 上一次结算的天数
        private int lastProcessedDay = -1;

        //工资补正
        private float correction = 0.2f;
        
        
        public WageProcessor(Map map) : base(map)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastProcessedDay, "lastProcessedDay", -1);
            Scribe_Values.Look(ref correction, "correction", 0.2f);
        }

        public override void MapComponentTick()
        {
            // 当前是第几天
            int currentDay = Find.TickManager.TicksGame / TicksPerDay;

            if (currentDay == lastProcessedDay)
                return;

            lastProcessedDay = currentDay;

            try
            {
                ProcessDailyWages();
            }
            catch (Exception ex)
            {
                Log.Error("[CES] 工资处理器崩溃:\n" + ex);
            }
        }

        /// <summary>
        /// 每日工资结算主逻辑
        /// </summary>
        private void ProcessDailyWages()
        {
            var economy = map.GetComponent<MapComponent_ColonyEconomy>();
            if (economy == null)
                return;

            foreach (Pawn pawn in map.mapPawns.FreeColonists)
            {
                ProcessPawnWage(pawn, economy);
            }
        }
        /// <summary>
        /// 处理单个 Pawn 的工资结算
        /// </summary>
        private void ProcessPawnWage(Pawn pawn, MapComponent_ColonyEconomy economy)
        {
            var data = pawn.GetEconomyData();
            if (data == null)
                return;
            
            var workSettings = pawn.workSettings;
            if (workSettings == null)
                return;
            
            foreach (WorkTypeDef workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
               
                
                // 1. 该工作是否启用
                if (!workSettings.WorkIsActive(workType))
                    continue;

                // 2. 获取该工作的主要技能
                SkillDef skillDef = workType.relevantSkills?.FirstOrDefault();
                if (skillDef == null)
                    continue;

                // 3. 读取 Pawn 的技能等级
                SkillRecord skill = pawn.skills.GetSkill(skillDef);
                if (skill == null)
                    continue;
                
                //得到技能等级，基础工资，工作优先级
                int skillLevel = skill.Level;
                int basePrice = WageUtility.GetBaseWage(workType);
                int priority = workSettings.GetPriority(workType);

                //只统计优先级1,2,3的工作,其他忽略。
                if (priority>2)
                {
                    continue;
                }
                // 4. 写入经济数据
                data.AddWork(workType, basePrice, skillLevel,priority);
            }
            
            float rawWage = data.CalculatePendingWage(data.workPriceByType);
            
            int wageToPay = Mathf.FloorToInt(rawWage);

            if (wageToPay <= 0)
                return;
            
            bool paid = economy.TryConsumeSilver(wageToPay);
            if (paid)
            {
                //加上待发工资和欠薪(如果有的话)
                data.AddMoney(wageToPay+(int)data.unpaidWage);
                data.ClearPendingWork(); // 清除工作列表
                data.unpaidWage = 0;//结清欠款
                //清空利润记录
                data.Profit = 0;

                // 核心代码：添加心情 Buff
                // 1. 获取 ThoughtDef
                ThoughtDef wageDef = DefDatabase<ThoughtDef>.GetNamed("CES_WageReceived");
                // 2. 添加记忆到 Pawn
                pawn.needs.mood.thoughts.memories.TryGainMemory(wageDef);
                
                Log.Message(
                    $"[CES] Paid wage | {pawn.NameShortColored} +{wageToPay}"
                );
            }
            else
            {
                data.AddUnpaid(wageToPay);

                Log.Warning(
                    $"[CES] Unpaid wage | {pawn.NameShortColored} owed {wageToPay}"
                );
            }
            
            //结算工资时顺便更新殖民者所有私人物品的溢价或降价
            foreach (var item in data.privateOwnedAssets)
            {
                item.priceChange+=Rand.Range(-0.5f, 0.5f);
            }
        }

        
    }
    
}
