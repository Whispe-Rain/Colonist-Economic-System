using System.Collections.Generic;
using RimWorld;
using Verse;

namespace EconomicSystem
{
    /// <summary>
    /// 单个殖民者的经济数据
    /// 负责：存储工作量 + 计算待支付工资
    /// </summary>
    public class CES_PawnEconomyData : IExposable
    {
        /// <summary>
        /// 殖民者的钱包余额
        /// </summary>
        public float virtualWallet;

        /// <summary>
        /// 尚未支付的工资（拖欠）
        /// </summary>
        public float unpaidWage;

        /// <summary>
        /// 按 WorkType 分类的累计工作量
        /// Key   : WorkTypeDef（建造 / 研究 / 清洁等）
        /// Value : 抽象工作量（不是 tick）
        /// </summary>
        public Dictionary<WorkTypeDef, float> workValueByType =
            new Dictionary<WorkTypeDef, float>();

        /// 殖民者的私人物品列表
        public List<PrivateItemData> privateOwnedAssets = new List<PrivateItemData>();

        //殖民者的经济信息记录
        public List<EconomicLogEntry> economicHistory = new List<EconomicLogEntry>();
        

        // 利息
        // 每日回馈率 (0.005f = 0.5% 每日)
        private const float DailyInterestRate = 0.005f;

        // 上次计算回馈的日期（以游戏天数计算）
        private int lastInterestDay = 0;

        #region 存档

        public void ExposeData()
        {
            Scribe_Values.Look(ref virtualWallet, "virtualWallet", 0f);
            Scribe_Values.Look(ref unpaidWage, "unpaidWage", 0f);
            Scribe_Collections.Look(

                ref workValueByType,
                "workValueByType",
                LookMode.Def,
                LookMode.Value
            );
            // ⭐ 更改存档逻辑以使用新的 PrivateItemData 列表
            Scribe_Collections.Look(
                ref privateOwnedAssets,
                "privateOwnedAssets",
                LookMode.Deep // 必须使用 Deep 模式来存档复杂对象
            );

            Scribe_Values.Look(ref lastInterestDay, "lastInterestDay", 0);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                privateOwnedAssets ??= new List<PrivateItemData>();
            }

            // 【关键修复】：确保列表的 Scribe 逻辑
            Scribe_Collections.Look(ref this.economicHistory, "economicHistory", LookMode.Deep);

            // 【安全检查】：如果加载失败，列表可能是 null，我们必须初始化它
            if (Scribe.mode == LoadSaveMode.LoadingVars && this.economicHistory == null)
            {
                this.economicHistory = new List<EconomicLogEntry>();
            }

        }

        #endregion

        #region 资产操作

        /// <summary>
        /// 增加虚拟钱包余额
        /// </summary>
        public void AddMoney(int amount)
        {
            if (amount <= 0)
                return;

            virtualWallet += amount;
        }

        /// <summary>
        /// 增加欠薪
        /// </summary>
        public void AddUnpaid(int amount)
        {
            if (amount <= 0)
                return;

            unpaidWage += amount;
        }

        /// <summary>
        /// 扣除虚拟钱包余额
        /// </summary>
        /// <returns>如果余额足够并成功扣款，返回 true；否则返回 false。</returns>
        public bool SubtractMoney(int amount)
        {
            if (amount <= 0)
                return true;

            // 注意：因为 virtualWallet 是 float 类型，我们进行浮点比较
            if (virtualWallet < amount)
                return false; // 余额不足

            virtualWallet -= amount;
            return true; // 扣款成功
        }

        /// <summary>
        /// 清空本结算周期的工作量
        /// </summary>
        public void ClearPendingWork()
        {
            workValueByType.Clear();
        }

        #endregion

        #region 工资计算核心

        /// <summary>
        /// 计算当前待支付工资（不取整）
        /// 由 WageProcessor 决定是否发放
        /// </summary>
        public float CalculatePendingWage()
        {
            float totalWage = 0f;

            foreach (var pair in workValueByType)
            {
                WorkTypeDef workType = pair.Key;
                float workAmount = pair.Value;

                if (workAmount <= 0f || workType == null)
                    continue;

                // 获取该工作类型的工资系数
                float wageFactor = GetWageFactor(workType);

                totalWage += workAmount * wageFactor;
            }

            return totalWage*CES_EconomyUtility.GetCorrection(Find.AnyPlayerHomeMap);
        }

        /// <summary>
        /// 得到单个工作类型的工作价值（工资）
        /// </summary>
        /// <param name="workType">工作类型</param>
        /// <returns>计算后的工资</returns>
        public float GetWageByWorkType(WorkTypeDef workType)
        {
            if (!workValueByType.ContainsKey(workType) || workValueByType[workType] < 0f)
                return 0f;
            return workValueByType[workType] * GetWageFactor(workType);
        }

        /// <summary>
        /// 根据 WorkType 返回工资系数
        /// ⚠ MVP 版本：硬编码
        /// 以后可以替换为 Def / ModSetting
        /// </summary>
        private float GetWageFactor(WorkTypeDef workType)
        {
            // 示例规则（你可以随时改）
            //研究价值
            if (workType == WorkTypeDefOf.Research)
                return 1.5f;

            //建造价值
            if (workType == WorkTypeDefOf.Construction)
                return 1.0f;

            //采矿价值
            if (workType == WorkTypeDefOf.Mining)
                return 1.0f;

            //清洁价值
            if (workType == WorkTypeDefOf.Cleaning)
                return 0.2f;

            //狩猎价值
            if (workType == WorkTypeDefOf.Hauling)
                return 1.2f;

            //监管价值
            if (workType == WorkTypeDefOf.Warden)
                return 1f;
            //医疗价值
            if (workType == WorkTypeDefOf.Doctor)
                return 1.7f;
            //制作/烹饪价值
            if (workType == WorkTypeDefOf.Crafting)
                return 0.8f;
            //锻造价值
            if (workType == WorkTypeDefOf.Smithing)
                return 1.2f;
            //割除价值
            if (workType == WorkTypeDefOf.PlantCutting)
                return 0.7f;
            //种植价值
            if (workType == WorkTypeDefOf.Growing)
                return 1.3f;
            //钓鱼价值
            if (workType == WorkTypeDefOf.Fishing)
                return 1.5f;

            // 默认工资系数
            return 0.3f;
        }

        public void AddWork(WorkTypeDef workType, float value)
        {
            if (workType == null || value <= 0f)
                return;

            if (!workValueByType.TryGetValue(workType, out float current))
                current = 0f;

            workValueByType[workType] = current + value;
        }

        /// <summary>
        /// 欠薪金额等级
        /// </summary>
        /// <param name="level">欠薪等级</param>
        /// <returns>该等级对应的金额</returns>
        public float GetUnpaidWageLevel(int level)
        {
            float unpaidwageCount = 0f;
            switch (level)
            {
                case 1:
                    unpaidwageCount = 200f;
                    break;
                case 2:
                    unpaidwageCount = 200f;
                    break;
            }

            return unpaidwageCount;
        }

        #endregion

        #region 物品绑定操作 (新的虚拟化操作)

        /// <summary>
        /// 将物品数据虚拟化并标记为私有资产。
        /// </summary>
        public void VirtualAndMarkAsset(Thing item)
        {
            if (item == null) return;

            // 1. 创建资产数据
            PrivateItemData asset = new PrivateItemData(item);

            // 2. 添加到列表
            privateOwnedAssets.Add(asset);

            // 3. 核心：从世界中移除物理物品
            if (item.Spawned)
            {
                item.DeSpawn(); // 从地图上移除
            }
            else if (item.holdingOwner != null)
            {
                // 从库存/容器中移除 (例如，如果 itemToTransfer 是 SplitOff 得到的临时对象)
                item.holdingOwner.Remove(item);
            }

            Log.Message(
                $"[CES_ASSET] Virtualized and destroyed item {item.LabelCap} (Count: {item.stackCount}). Total assets: {privateOwnedAssets.Count}.");
        }

        // 示例：从私有资产中移除一个项目 (用于出售或使用)
        public bool RemoveAsset(PrivateItemData asset)
        {
            return privateOwnedAssets.Remove(asset);
        }

        public bool AddAsset(PrivateItemData asset)
        {
            int i = privateOwnedAssets.Count;
            this.privateOwnedAssets.Add(asset);
            if (privateOwnedAssets.Count > i)
            {
                return true;
            }

            return false;
        }

        #endregion

        /// <summary>
        /// 检查并应用每日安全保管费回馈，将虚拟资金注入钱包。
        /// </summary>
        public void TryApplyDailyInterest()
        {
            // RimWorld 时间单位：60000 ticks = 1 Day
            int currentDay = GenTicks.TicksAbs / 60000;

            // 检查是否在同一天重复计算
            if (currentDay <= lastInterestDay)
            {
                return;
            }

            // 只有当钱包余额大于 50 银时才计算回馈，鼓励储蓄
            if (virtualWallet < 50f)
            {
                lastInterestDay = currentDay;
                return;
            }

            // 计算回馈金额（这是凭空产生的虚拟货币）
            float interestAmount = virtualWallet * DailyInterestRate;

            // 利息/回馈入账
            virtualWallet += interestAmount;

            // 更新日志
            lastInterestDay = currentDay;

        }
    }
}
