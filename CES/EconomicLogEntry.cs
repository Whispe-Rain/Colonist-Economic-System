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


        public EconomicLogEntry(){}

        public void ExposeData()
        {
            Scribe_Values.Look(ref message, "message");
        }
        
        /// <summary>
        /// 创建一个新的经济信息
        /// </summary>
        /// <param name="msg">文本</param>
        /// <param name="logColor">颜色</param>
        /// <returns></returns>
        public static EconomicLogEntry NewLog(string msg)
        {
            return new EconomicLogEntry
            {
                message = msg.Colorize(Color.white)
            };
        }
        
    }
    
    
    
}