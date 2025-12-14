using System.Collections.Generic;
using Verse;

//经济数据类
namespace EconomicSystem
{
    /// <summary>
    /// 单个殖民者的经济数据容器
    /// 
    /// ⚠ 注意：
    /// 1. 这个类【不会】被 RimWorld 自动存档
    /// 2. 只有当它被 GameComponent / MapComponent 持有时
    ///    ExposeData() 才会被调用
    /// </summary>
    public class CES_ColonistEconomyData : IExposable
    {
        // 累计工作价值（用于工资计算）
        public float totalWorkValue;
        //殖民者的虚拟钱包余额
        public float virtualWallet;
        //尚未支付的工资（拖欠）
        public float unpaidWage;

        /// <summary>
        /// 最近一次执行的工作类型
        /// 注意：这是 1.6 中判断工作归属的关键
        /// </summary>
        public WorkTypeDef lastWorkType;
        /// <summary>
        /// 按工作类型分类的工作价值统计
        /// Key: WorkTypeDef（研究、建造等）
        /// Value: 该类型累计工作价值
        /// </summary>
        public Dictionary<WorkTypeDef, float> workValueByType =
            new Dictionary<WorkTypeDef, float>();

        /// <summary>
        /// RimWorld 存档系统入口
        /// 
        /// 在以下情况被调用：
        /// - 上层对象（如 GameComponent）正在存档
        /// - 上层对象正在从存档中读取数据
        /// 
        /// Scribe 会根据当前模式（Saving / Loading）
        /// 自动决定是写入还是读取
        /// </summary>
        public void ExposeData()
        {
            //保存值类型数据
            Scribe_Values.Look(ref totalWorkValue, "totalWorkValue", 0f);
            Scribe_Values.Look(ref virtualWallet, "virtualWallet", 0f);
            Scribe_Values.Look(ref unpaidWage, "unpaidWage", 0f);
            
            //保存字典类型数据
            Scribe_Collections.Look(
                ref workValueByType,
                "workValueByType",
                LookMode.Def,
                LookMode.Value
            );
        }
    }
}