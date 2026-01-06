using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace EconomicSystem
{
    
    public struct WorkWagePreview
    {
        //工作类型
        public WorkTypeDef workType;
        //基础工资
        public int basePrice;
        //相关技能等级
        public int skillLevel;
        //优先级系数
        public int priority;
    }
    
    /// <summary>
    /// 单个殖民者的经济数据
    /// 负责：存储工作量 + 计算待支付工资
    /// </summary>
    public class CES_PawnEconomyData : IExposable
    {
        
        /// <summary>
        /// 殖民者的钱包余额
        /// </summary>
        public float virtualWallet=200f;

        /// <summary>
        /// 尚未支付的工资（拖欠）
        /// </summary>
        public float unpaidWage;

        /// <summary>
        /// 按 WorkType 分类的工作量
        /// </summary>
        public List<WorkWagePreview> workPriceByType = new List<WorkWagePreview>();
        
        //UI用
        public List<WorkWagePreview> dailyWorkPreview = new List<WorkWagePreview>();
        
        /// 殖民者的私人物品列表
        public List<PrivateItemData> privateOwnedAssets = new List<PrivateItemData>();

        public int Profit;
        //殖民者的经济信息记录
        public List<EconomicLogEntry> economicHistory = new List<EconomicLogEntry>();
        
        //交易冷却
        // Key: 冷却行为的类型 (e.g., "TradeFail", "StealFail")
        // Value: 冷却结束的 Tick 时间
        public Dictionary<string, int> coolDowns = new Dictionary<string, int>();

        //个人所得税
        public float Tax;
        
        //外贸购物CD
        public int ForeignShoppingTick;
        
        

        #region 存档

        public void ExposeData()
        {
            Scribe_Values.Look(ref virtualWallet, "virtualWallet", 0f);
            Scribe_Values.Look(ref unpaidWage, "unpaidWage", 0);
            
            
            // ⭐ 更改存档逻辑以使用新的 PrivateItemData 列表
            Scribe_Collections.Look(
                ref privateOwnedAssets,
                "privateOwnedAssets",
                LookMode.Deep // 必须使用 Deep 模式来存档复杂对象
            );
            

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                privateOwnedAssets ??= new List<PrivateItemData>();
            }

            // 【关键修复】：确保列表的 Scribe 逻辑
            Scribe_Collections.Look(
                ref this.economicHistory, 
                "economicHistory", 
                LookMode.Deep);

            // 【安全检查】：如果加载失败，列表可能是 null，我们必须初始化它
            if (Scribe.mode == LoadSaveMode.LoadingVars && this.economicHistory == null)
            {
                this.economicHistory = new List<EconomicLogEntry>();
            }
            
            Scribe_Collections.Look(
                ref this.coolDowns, 
                "coolDowns", 
                LookMode.Value,
                LookMode.Value);
            
            // 【安全检查】：如果加载失败，列表可能是 null，我们必须初始化它
            if (Scribe.mode == LoadSaveMode.LoadingVars && this.coolDowns == null)
            {
                this.coolDowns = new Dictionary<string, int>();
            }
            
            Scribe_Values.Look(ref Profit, "Profit", 0);
            //初始值为钱包的5%
            Scribe_Values.Look(ref Tax, "Tax", virtualWallet*0.05f);
            Scribe_Values.Look(ref ForeignShoppingTick, "ForeignShoppingTick", 0);
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
            workPriceByType.Clear();
        }

        #endregion

        #region 工资计算核心

        /// <summary>
        /// 计算当前待支付工资（不取整）
        /// 由 WageProcessor 决定是否发放
        /// </summary>
        /// <param name="previews"></param>
        public float CalculatePendingWage(List<WorkWagePreview> previews)  
        {
            float totalWage = 0f;

            foreach (var pair in previews)
            {
                //计算单个工作的工资
                float workPrice = pair.basePrice *
                                  WageUtility.GetSkillFactor(pair.skillLevel).Item2*
                                  WageUtility.GetPriorityMultiplier(pair.priority);
                
                if (workPrice <= 0f || pair.workType == null)
                    continue;

                totalWage +=workPrice*CES_EconomyUtility.GetCorrection(Find.AnyPlayerHomeMap);
            }
            //总工资*工资系数补正
            return totalWage;
        }

        ///  <summary>
        /// 得到某个工作的工资(包含工资系数)
        ///  </summary>
        ///  <param name="workType"></param>
        ///  <param name="previews"></param>
        ///  <returns></returns>
        public float GetWage(WorkTypeDef workType,List<WorkWagePreview> previews)
        {
            foreach (var pair in previews)
            {
                if (pair.workType == workType)
                {
                    float workPrice = pair.basePrice *
                                      WageUtility.GetSkillFactor(pair.skillLevel).Item2*
                                      WageUtility.GetPriorityMultiplier(pair.priority);
                    return workPrice;
                }
            }
            Log.Warning($"没有找到{workType}工作");
            return 0f;
            
        }

        

        /// <summary>
        /// 添加工作
        /// </summary>
        public void AddWork(WorkTypeDef workType, int basePrice, int skillLevel, int priority)
        {
            if (workType == null)
                return;

            WorkWagePreview preview = new WorkWagePreview
            {
                workType = workType,
                basePrice = basePrice,
                skillLevel = skillLevel,
                priority = priority
            };

            // 结算用
            workPriceByType.Add(preview);

            // UI 用（如果不存在）
            if (!dailyWorkPreview.Any(p => p.workType == workType))
            {
                dailyWorkPreview.Add(preview);
            }
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
        public void VirtualAndMarkAsset(Thing item,int count)
        {
            if (item == null) return;

            // 1. 创建资产数据
            PrivateItemData asset = new PrivateItemData(item);
            //保存数量
            asset.stackCount = count;
            
            

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
            
            //如果已经存在该物品，只增加数量
            foreach (var itemData in privateOwnedAssets)
            {
                if (itemData.defName==item.def.defName)
                {
                    itemData.stackCount+=asset.stackCount;
                    return;
                }
               
            }
            // 2. 添加到列表
            privateOwnedAssets.Add(asset);
        }

        // 示例：从私有资产中移除一个项目 (用于出售或使用)
        public bool RemoveAsset(PrivateItemData asset,int sellPrice=0)
        {
            //记录售出价
            asset.sellPrice=sellPrice;
            
            return privateOwnedAssets.Remove(asset);
        }

        public bool AddAsset(PrivateItemData asset,int buyPrice=0)
        {
            int i = privateOwnedAssets.Count;
            //记录买入价格
            asset.buyPrice=buyPrice;

            //遍历列表，查找是否有同一个名字的物品
            foreach (var itemData in privateOwnedAssets)
            {
                //有同名物品
                if (itemData.defName==asset.defName)
                {
                    //不创建新物品,直接增加数量。
                    itemData.stackCount+=asset.stackCount;
                    return true;
                }
            }
            this.privateOwnedAssets.Add(asset);
            
            if (privateOwnedAssets.Count > i)
            {
                return true;
            }

            return false;
        }
        
        public int GetProfit(int sellPrice, int buyPrice)
        {
            return sellPrice-buyPrice;
        }

        #endregion
        
        /// <summary>
        /// 添加日志
        /// </summary>
        /// <param name="historys">日志合集</param>
        /// <param name="log">要添加的日志内容</param>
        public void AddHistory(List<EconomicLogEntry> historys,String log)
        {
            //如果当前日志条数超过了50条
            if (historys.Count>=50)
            {
                //清空日志
                historys.Clear();
            }
            //添加新日志
            historys.Add(EconomicLogEntry.NewLog(log));
        }
    }
}
