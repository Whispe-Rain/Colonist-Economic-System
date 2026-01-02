using RimWorld;
using UnityEngine;
using Verse;

namespace EconomicSystem
{
    public  class WageUtility
    {
        
        public static int GetBaseWage(WorkTypeDef workType)
        {
            // 示例规则（你可以随时改）
            //研究价值
            if (workType == WorkTypeDefOf.Research)
                return 22;

            //建造价值
            if (workType == WorkTypeDefOf.Construction)
                return 18;

            //采矿价值
            if (workType == WorkTypeDefOf.Mining)
                return 12;

            //清洁价值
            if (workType == WorkTypeDefOf.Cleaning)
                return 6;
            
            //搬运价值
            if (workType==WorkTypeDefOf.Hauling)
                return 6;

            //狩猎价值
            if (workType == WorkTypeDefOf.Handling)
                return 13;

            //监管价值
            if (workType == WorkTypeDefOf.Warden)
                return 15;
            //医疗价值
            if (workType == WorkTypeDefOf.Doctor)
                return 22;
            //制作/烹饪价值
            if (workType == WorkTypeDefOf.Crafting)
                return 18;
            //锻造价值
            if (workType == WorkTypeDefOf.Smithing)
                return 18;
            //割除价值
            if (workType == WorkTypeDefOf.PlantCutting)
                return 8;
            //种植价值
            if (workType == WorkTypeDefOf.Growing)
                return 14;
            //钓鱼价值
            if (workType == WorkTypeDefOf.Fishing)
                return 14;
            //保育价值
            if (workType == WorkTypeDefOf.Childcare)
                return 8;
            //不需要技能要求的工作，不支付工资。
            if (workType.relevantSkills.Count==0)
                return 0;

            // 其他工资系数，包括MOD添加的
            return 10;
        }

        /// <summary>
        /// 技能等级对工资的影响
        /// </summary>
        /// <returns>（技能评级,技能系数）</returns>
        public static (string,float) GetSkillFactor( int SkillLevel)
        {
            if (SkillLevel>=0&&SkillLevel<=3)
            {
                return  ("见习".Colorize(Color.gray),0.5f);
            }
            else if (SkillLevel>=4&&SkillLevel<=7)
            {
                return ("入门".Colorize(Color.white),0.8f);
            }
            
            else if (SkillLevel>=8&&SkillLevel<=10)
            {
                return  ("标准".Colorize(Color.green),1f);
            }
            else if (SkillLevel>=11&&SkillLevel<=14)
            {
                return  ("熟练".Colorize(Color.blue),1.3f);
            }
            else if (SkillLevel>=15&&SkillLevel<=17)
            {
                return  ("专家".Colorize(Color.magenta),1.6f);
            }
            else if (SkillLevel>=18&&SkillLevel<=20)
            {
                return ("大师".Colorize(Color.yellow),2f);
            }
            else
            {
                return ("不存在",0f);
            }
        }
        
        //工资优先级补正
        public static float GetPriorityMultiplier(int priority)
        {
            return priority switch
            {
                1 => 1.0f,
                2 => 0.7f,
                _ => 0f   // 3 = 关闭 / 不计薪
            };
        }
        
    }
}