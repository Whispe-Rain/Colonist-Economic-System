using System.Runtime.CompilerServices;
using Verse;

//角色经济扩展
//为Pawn增加了经济数据的扩展方法
namespace EconomicSystem
{
    public static class CES_PawnEconomyExtension
    {
        //.NET 官方提供的“附加数据表”，“当 key（Pawn）被 GC 回收时，value（经济数据） 自动消失”
        private static readonly ConditionalWeakTable<Pawn, CES_ColonistEconomyData>
            dataTable = new();
        
        /// <summary>
        /// 最近一次执行的工作类型
        /// 注意：这是 1.6 中判断工作归属的关键
        /// </summary>
        
        
        //得到Pawn的经济数据
        public static CES_ColonistEconomyData GetEconomyData(this Pawn pawn)
        {
            //pawn为空，或者不是殖民者。忽略
            if (pawn == null || !pawn.IsColonist)
                return null;

            //返回该pawn的经济数据
            return dataTable.GetValue(
                pawn,
                p => new CES_ColonistEconomyData()
            );
        }
    }
}