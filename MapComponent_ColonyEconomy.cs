using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace EconomicSystem
{
    /// <summary>
    /// 殖民地经济组件（绑定到 Map）
    /// 
    /// 职责：
    /// - 维护殖民地的“虚拟白银余额”
    /// - 提供统一的支付 / 入账接口
    /// - 支持存档
    /// 
    /// 不负责：
    /// - 工作统计
    /// - 工资计算
    /// - UI 显示
    /// </summary>
    public class MapComponent_ColonyEconomy : MapComponent
    {

        //public float colonySilverBalance;
        /// <summary>
        /// 是否已经从地图实体白银初始化过
        /// （防止重复扫描）
        /// </summary>
        private bool initialized;
        
        // 构造
        public MapComponent_ColonyEconomy(Map map) : base(map)
        {
        }
        
        // 存档
        public override void ExposeData()
        {
            base.ExposeData();

            //Scribe_Values.Look(ref colonySilverBalance, "colonySilverBalance", 0f);
            Scribe_Values.Look(ref initialized, "initialized", false);
        }
        
        // 初始化
        /// <summary>
        /// 在地图加载完成后调用一次
        /// 用地图上的实体白银初始化虚拟余额
        /// </summary>
        public override void FinalizeInit()
        {
            base.FinalizeInit();

            if (initialized)
                return;

            GetTotalSilver();
            initialized = true;
        }
        /// <summary>
        /// 尝试扣除殖民地的白银
        /// </summary>
        /// <param name="amount">扣除数量</param>
        /// <returns>是否扣除成功</returns>
        public bool TryConsumeSilver(int amount)
        {
            int remaining = amount;

            //var silvers = map.listerThings.ThingsOfDef(ThingDefOf.Silver);
            
            // ⚠ 关键：复制一份列表
            List<Thing> silvers = map.listerThings
                .ThingsOfDef(ThingDefOf.Silver)
                .ToList();
            
            foreach (var silver in silvers)
            {
                if (remaining <= 0)
                    break;

                int take = Mathf.Min(silver.stackCount, remaining);
                silver.stackCount -= take;
                remaining -= take;

                if (silver.stackCount <= 0)
                    silver.Destroy();
            }

            return remaining <= 0;
        }

        /// <summary>
        /// 得到当前殖民地白银数量
        /// </summary>
        /// <returns></returns>
        public int GetTotalSilver()
        {
            return map.listerThings
                .ThingsOfDef(ThingDefOf.Silver)
                .Sum(t => t.stackCount);
        }
    }
}
