using System.Collections.Generic;
using System.Linq;
using EconomicSystem.Transaction_交易_;
using Gastronomy.Restaurant;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace EconomicSystem
{
    public class JoyGiver_Transaction:JoyGiver
    {
        // 交易的最远距离，防止追逐太远
        private const float MaxTradeDistance = 40f;
        
        
        
        //决定了Pawn执行交易行为的概率
        public override float GetChance(Pawn pawn)
        {
            //只对殖民者有效
            if (pawn==null||!pawn.IsColonist)
            {
                return 0f;
            }
            
            CES_PawnEconomyData data=pawn.GetEconomyData();
            //检测该Pawn的私人物品中是否有可以交易的物品
            List<PrivateItemData> privateItems = data.privateOwnedAssets;
            
            if (data == null || data.privateOwnedAssets.NullOrEmpty())
            {
                return 0f;
            }
            
            //概率计算
            float baseShoppingChance = 0.25f; 
            
            //财富值越高越不容易售卖物品，反之越容易出售物品
            float walletScale = Mathf.InverseLerp(3000f, 50f, data.virtualWallet); 
    
            // 将 0~1 的 walletScale 映射到 0.5~2.0 的因子范围 (线性映射)
            float walletFactor = 0.5f + walletScale * 1.5f; 
    
            // 最终概率
            float finalChance = baseShoppingChance * walletFactor;

            //Log.Message($"[CES_DEBUG] {pawn.NameShortColored} GetChance: 基础概率={baseShoppingChance:P2}, 财富因素={walletFactor:F2}. 最终出售概率={finalChance:P2}.");
    
            // 确保概率不超过 1.0
            return Mathf.Clamp01(finalChance); 
            
        }
        
        public override Job TryGiveJob(Pawn pawn)
        {
            CES_PawnEconomyData data=pawn.GetEconomyData();
            //得到全部殖民者
            List<Pawn> allCustomer = TransactionUtility.FindAllTheCustomers(pawn);
            if (allCustomer == null || allCustomer.NullOrEmpty())
            {
                return null;
            }
            
            // 检查通用交易冷却（例如，刚才拒绝了某个商人的交易）
            if (data.IsOnCooldown("TransactionFail"))
            {
                return null;
            }
            
            //1.确认代售商品
            PrivateItemData GoodsData= data.privateOwnedAssets[Random.Range(0, data.privateOwnedAssets.Count)];
            Thing Goods = GoodsData.RecreateThing();
            
            // --- 日志 1: 尝试触发 Job ---
            //Log.Message($"[CES_DEBUG] {pawn.NameShortColored} 正在尝试进行交易...");
            
            //2.得到全部客户
            List<Pawn> customers=new List<Pawn>();
            foreach (Pawn customer in allCustomer)
            {
                
                // 1. 检查是否可以预定
                if (!pawn.CanReserve(customer, 1, -1))
                {
                    continue; // 无法预定，尝试下一个角色
                }
                
                float estimatedValue =
                    Goods.MarketValue * GoodsData.stackCount * 2f;
                //客户的钱包必须要能支付的起该商品市场价2倍并且在40格范围内
                if (customer.GetEconomyData().virtualWallet<estimatedValue||
                    !pawn.Position.InHorDistOf(customer.Position, MaxTradeDistance)
                    )
                {
                    continue;
                }
                customers.Add(customer);
            }
            //没找到用户直接取消用户
            if (customers.Count==0)
            {
                Log.Message(pawn.NameShortColored+"没能找到可以售卖的人");
                return null;
                   
            }
            //Log.Message("潜在客户数量："+customers);
            // 3.随机选择一个初始目标 (作为 Job 的 TargetA)
            Pawn initTarget = customers.RandomElement();
            
            // 创建 Job 实例
            Job job = JobMaker.MakeJob(this.def.jobDef, initTarget);
            //得到待售商品的名字，使其通过Job传到JobDriver中去
            job.dutyTag = GoodsData.defName;
            return job;
        }
        
    }
}