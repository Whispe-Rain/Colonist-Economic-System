using System.Collections.Generic;
using System.Linq;
using Verse;

namespace EconomicSystem.Transaction_交易_
{
    
    public class TransactionUtility
    {
        /// <summary>
        /// 找到所有可能成为买家的殖民者
        /// </summary>
        /// <param name="pawn">卖家</param>
        /// <returns>买家群</returns>
        public static List<Pawn> FindAllTheCustomers(Pawn pawn)
        {
            if (pawn == null||pawn.Map == null)
            {
                return null;
            }

            //得到地图上所有的Pawn
            List<Pawn> allPawns = pawn.Map.mapPawns.AllPawns;
            
            //保存全部殖民者
            List<Pawn> allColonist = new List<Pawn>();
            foreach (Pawn pawn2 in allPawns)
            {
                // 1. 排除卖家自己
                if (pawn2 == pawn) continue; 
    
                // 2. 只筛选殖民者（或友军），并且要求有经济数据
                CES_PawnEconomyData data = pawn2.TryGetEconomyData();
                if (pawn2.IsColonist && data != null && data.virtualWallet > 15) 
                {
                    allColonist.Add(pawn2);
                }
            }

            //筛选出殖民者
            return allColonist;
        }
        
    }
}