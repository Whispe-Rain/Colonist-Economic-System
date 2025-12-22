using System;
using UnityEngine;
using Verse;

namespace EconomicSystem
{
    /// <summary>
    /// 经济信息数据类
    /// </summary>
    public class EconomicLogEntry:IExposable
    {
        public string message; // 例如: "Bought 5 Fine Meal from [Pawn/TraderName] for 30 silver."
        public Color color = Color.white; // 可选: 用于区分买入/卖出


        public EconomicLogEntry(){}

        public void ExposeData()
        {
            Scribe_Values.Look(ref message, "message");
            Scribe_Deep.Look(ref color, "color");
        }
        
        /// <summary>
        /// 创建一个新的经济信息
        /// </summary>
        /// <param name="msg">文本</param>
        /// <param name="logColor">颜色</param>
        /// <returns></returns>
        public static EconomicLogEntry NewLog(string msg, Color? logColor = null)
        {
            return new EconomicLogEntry
            {
                message = msg,
                color = logColor ?? Color.white 
            };
        }
        
    }
    
    
    
}