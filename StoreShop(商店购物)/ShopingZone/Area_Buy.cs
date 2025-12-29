using RimWorld;
using Verse;
using UnityEngine;

namespace EconomicSystem
{
    public class Area_Buy : Area
    {
        public Area_Buy()
        {
        }
        public Area_Buy(AreaManager areaManager) : base(areaManager)
        {
            // 允许玩家重命名（很重要）
            this.RenamableLabel = "购物区";
        }

        // === 必须实现 1：显示名称 ===
        public override string Label => "购物区";

        // === 必须实现 2：区域颜色 ===
        public override Color Color => new Color(0.25f, 0.85f, 0.35f, 0.25f);

        // === 必须实现 3：Area 列表排序优先级 ===
        // 数值越小越靠前，原版通常在 0 ~ 100
        public override int ListPriority => 50;

        // === 必须实现 4：存档唯一 ID ===
        public override string GetUniqueLoadID()
        {
            // Area 的标准写法：类型名 + ID
            return $"Area_Buy_{this.ID}";
        }

        // （可选）是否能被“允许区域”系统使用
        public override bool AssignableAsAllowed()
        {
            return false;
        }
    }
}