using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace EconomicSystem
{
    // 存储私有物品的完整状态，用于虚拟化资产
    public class PrivateItemData : IExposable
    {
        public string defName;      // 物品定义 ID (例如: Steel)
        public int stackCount;      // 数量
        public float hitPointsPct;  // 耐久度百分比 (0.0 - 1.0)
        public QualityCategory? quality; // 品质 (如果物品有品质)
        public string stuffDefName; // 材质 Def (例如: Plasteel)
        // 可以根据需要添加更多属性（例如，制造者、名称等）

        // 默认构造函数 (用于 Scribe)
        public PrivateItemData() { }
        
        //得到物品的具体名字
        public TaggedString Name => this.RecreateThing().def.LabelCap;

        // 从现有 Thing 实例化的构造函数
        public PrivateItemData(Thing t)
        {
            if (t == null) return;

            defName = t.def.defName;
            stackCount = t.stackCount;
            hitPointsPct = (float)t.HitPoints / t.MaxHitPoints;
            // 获取品质
            if (t.TryGetQuality(out QualityCategory qc))
            {
                quality = qc;
            }
            
            // 获取材质
            if (t.def.MadeFromStuff)
            {
                stuffDefName = t.Stuff?.defName;
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref defName, "defName");
            Scribe_Values.Look(ref stackCount, "stackCount");
            Scribe_Values.Look(ref hitPointsPct, "hitPointsPct", 1f); // 默认 100%

            // Scribe_Values.Look 无法直接处理 Nullable<Enum>，需要特殊处理
            QualityCategory tmpQuality = quality ?? QualityCategory.Normal;
            Scribe_Values.Look(ref tmpQuality, "quality", QualityCategory.Normal);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                quality = tmpQuality;
            }
            
            Scribe_Values.Look(ref stuffDefName, "stuffDefName");
        }

        /// <summary>
        /// 从存储的数据重建物品实例 (用于面板展示或按需生成)
        /// </summary>
        // 在 PrivateItemData.cs 文件中
        public Thing RecreateThing()
        {
            // 修复 1: 使用 DefDatabase<ThingDef>.GetNamedSilentFail
            // 这样如果 Def 不存在，它会返回 null 且不会在 Log 中抛出红色错误
            ThingDef thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(defName); 
    
            if (thingDef == null)
            {
                Log.Error($"[CES] Failed to recreate PrivateItemData. Unknown ThingDef: {defName}");
                return null;
            }

            ThingDef stuffDef = null;
            if (!stuffDefName.NullOrEmpty())
            {
                // 修复 2: 同样使用 DefDatabase<ThingDef>.GetNamedSilentFail
                stuffDef = DefDatabase<ThingDef>.GetNamedSilentFail(stuffDefName);
        
                // 可选的安全检查: 确保材质 Def 是有效的
                if (stuffDef == null)
                {
                    Log.Warning($"[CES] Stuff Def {stuffDefName} not found when recreating {defName}. Proceeding without stuff.");
                }
            }

            // 1. 创建基础物品
            // 注意：ThingMaker.MakeThing 接受 null 材质，所以即使 stuffDef 为 null 也是安全的
            Thing newThing = ThingMaker.MakeThing(thingDef, stuffDef);
            newThing.stackCount = stackCount;

            // 2. 恢复品质
            if (quality.HasValue && newThing.TryGetComp<CompQuality>() is CompQuality compQ)
            {
                compQ.SetQuality(quality.Value, ArtGenerationContext.Colony);
            }
    
            // 3. 恢复耐久度
            if (hitPointsPct < 1f)
            {
                newThing.HitPoints = Mathf.RoundToInt(newThing.MaxHitPoints * hitPointsPct);
            }

            // 4. TODO: 恢复风格、名称等

            return newThing;
        }
    }
}