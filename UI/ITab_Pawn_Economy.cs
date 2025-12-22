using RimWorld;
using UnityEngine;
using Verse;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EconomicSystem
{
    
    
    
    
    // 殖民者“经济”选项卡界面
    public class ITab_Pawn_Economy : ITab
    {
        // 左侧面板 (工资构成) 的滚动状态
        private Vector2 scrollPos = Vector2.zero;
        private float viewHeight;

        // ⭐ 新增: 右侧面板 (虚拟背包) 的滚动状态
        private Vector2 scrollPosVirtual = Vector2.zero;
        private float viewHeightVirtual;

        private Vector2 scrollPosLog = Vector2.zero; // ⭐ 新增：用于 Log 面板的滚动位置
        
        // 健壮的 Pawn 获取属性
        private Pawn Pawn
        {
            get
            {
                Pawn result = base.SelPawn;
                if (result == null && base.SelThing is Corpse corpse)
                {
                    result = corpse.InnerPawn;
                }

                return result;
            }
        }

        public ITab_Pawn_Economy()
        {
            // ⭐ 关键修改：增加宽度和高度以容纳双列
            size = new Vector2(600f, 400f);
            labelKey = "经济";
        }

        public override bool IsVisible
        {
            get
            {
                Pawn pawn = this.Pawn;
                return pawn != null && pawn.IsColonist && !pawn.Dead;
            }
        }

        protected override void FillTab()
        {
            Rect rect = new Rect(0f, 0f, size.x, size.y).ContractedBy(10f);

            Pawn pawn = Pawn;
            var data = pawn?.GetEconomyData();

            if (pawn == null || data == null)
            {
                Widgets.Label(rect, "数据获取失败。");
                return;
            }
    
            // --- 【关键修正】 三等分区域划分 ---
            float totalWidth = rect.width;
            float gap = 10f; 
    
            // 计算每列的宽度 (减去两个间隙后，除以三)
            float colWidth = (totalWidth - (gap * 2)) / 3f; 

            // 1. 左侧面板：经济 (钱包/工资)
            Rect leftRect = new Rect(rect.x, rect.y, colWidth, rect.height);

            // 2. 中间面板：日志/记录
            float centerX = leftRect.xMax + gap;
            Rect centerRect = new Rect(centerX, rect.y, colWidth, rect.height);
    
            // 3. 右侧面板：私人资产
            float rightX = centerRect.xMax + gap;
            Rect rightRect = new Rect(rightX, rect.y, colWidth, rect.height);
    
            // 绘制垂直分隔线
            Widgets.DrawLineVertical(leftRect.xMax + gap / 2f, rect.y, rect.height); // 左/中分隔线
            Widgets.DrawLineVertical(centerRect.xMax + gap / 2f, rect.y, rect.height); // 中/右分隔线

            // 调用绘制函数
            DrawEconomicPanel(leftRect, pawn, data);
            DrawEconomicLogPanel(centerRect, pawn, data); // ⭐ 中间是 Log
            DrawVirtualAssetPanel(rightRect, pawn, data);  // ⭐ 右侧是资产
        }

        /// <summary>
        /// 绘制左侧面板：钱包、欠薪和工资构成
        /// </summary>
        private void DrawEconomicPanel(Rect rect, Pawn pawn, CES_PawnEconomyData data)
        {
            float lineHeight = 28f;
            float curY = rect.yMin;

            // ===== 标题 =====
            Widgets.Label(rect.TopPartPixels(lineHeight), "经济");
            curY += lineHeight + 10f;

            // ===== 顶部数值 =====
            Widgets.Label(new Rect(rect.x, curY, rect.width, lineHeight),
                $"钱包：{data.virtualWallet:F0}");
            curY += lineHeight;

            Widgets.Label(new Rect(rect.x, curY, rect.width, lineHeight),
                $"欠薪：{data.unpaidWage:F0}");
            curY += lineHeight;

            float totalWage = data.CalculatePendingWage();
            Widgets.Label(new Rect(rect.x, curY, rect.width, lineHeight),
                $"待发工资：{totalWage:F0}");
            curY += lineHeight + 6f;

            // --- 分隔线 ---
            Widgets.DrawLineHorizontal(rect.x, curY, rect.width);
            curY += 10f;

            Widgets.Label(new Rect(rect.x, curY, rect.width, lineHeight), "工资构成");
            curY += lineHeight;

            // ===== ScrollView 准备 =====
            Rect scrollRect = new Rect(
                rect.x,
                curY,
                rect.width,
                rect.yMax - curY
            );

            float rowHeight = 26f;
            float viewY = 0f;

            var workSettings = pawn.workSettings;
            if (workSettings == null)
            {
                Widgets.Label(scrollRect, "无工作数据");
                return;
            }

            // ⭐ 先计算 viewHeight
            foreach (WorkTypeDef workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                if (!workSettings.WorkIsActive(workType))
                    continue;

                if (data.GetWageByWorkType(workType) > 0.01f)
                    viewY += rowHeight;
            }

            viewHeight = viewY + 10f;

            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, viewHeight);

            Widgets.BeginScrollView(scrollRect, ref scrollPos, viewRect);

            // ===== 真正绘制行 =====
            float drawY = 0f;

            foreach (WorkTypeDef workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                if (!workSettings.WorkIsActive(workType))
                    continue;

                float wage = data.GetWageByWorkType(workType);
                if (wage <= 0.01f)
                    continue;

                int priority = workSettings.GetPriority(workType);

                Rect row = new Rect(0f, drawY, viewRect.width, rowHeight);

                // ===== 文字颜色 =====
                string label = workType.labelShort ?? workType.label;
                if (priority == 1)
                    label = ("★ " + label).Colorize(Color.white);
                else if (priority >= 4)
                    label = label.Colorize(Color.gray);

                Rect labelRect = new Rect(row.x, row.y, row.width * 0.35f, row.height);
                Widgets.Label(labelRect, label);

                // ===== 占比条 =====
                Rect barRect = new Rect(
                    labelRect.xMax + 6f,
                    row.y + 6f,
                    row.width * 0.4f,
                    row.height - 12f
                );

                float pct = totalWage > 0f ? wage / totalWage : 0f;
                Widgets.FillableBar(barRect, pct);
                Widgets.DrawBox(barRect);

                // ===== 工资数值 =====
                Rect valueRect = new Rect(barRect.xMax + 6f, row.y, row.width * 0.2f, row.height);
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(valueRect, wage.ToString("F0"));
                Text.Anchor = TextAnchor.UpperLeft;

                TooltipHandler.TipRegion(row,
                    $"{workType.label}\n累计工资：{wage:F0}");

                drawY += rowHeight;
            }

            Widgets.EndScrollView();
        }

        /// <summary>
        /// 绘制右侧面板：私有虚拟资产 (虚拟背包)
        /// </summary>
        private void DrawVirtualAssetPanel(Rect rect, Pawn pawn, CES_PawnEconomyData data)
        {
            float lineHeight = 28f;
            float curY = rect.yMin;

            // ===== 标题 =====
            Widgets.Label(rect.TopPartPixels(lineHeight), "资产");
            curY += lineHeight + 5f;

            var assets = data.privateOwnedAssets; // ⭐ 访问新的资产列表

            if (!assets.Any())
            {
                Widgets.Label(new Rect(rect.x, curY, rect.width, 30f), "该殖民者没有私有资产。");
                return;
            }

            // ===== ScrollView 准备 =====
            Rect scrollRect = new Rect(
                rect.x,
                curY,
                rect.width,
                rect.yMax - curY
            );

            float iconSize = 36f; // 物品图标大小
            float rowHeight = 40f; // 物品行高

            // 1. 计算 viewHeightVirtual
            viewHeightVirtual = assets.Count * rowHeight + 10f;

            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, viewHeightVirtual);

            Widgets.BeginScrollView(scrollRect, ref scrollPosVirtual, viewRect);

            // ===== 真正绘制行 =====
            float drawY = 0f;

            foreach (PrivateItemData assetData in assets)
            {
                Rect row = new Rect(0f, drawY, viewRect.width, rowHeight);

                // 尝试重建一个临时的 Thing 实例用于图标和工具提示 (重要!)
                Thing tempThing = assetData.RecreateThing();

                if (tempThing == null)
                {
                    Widgets.Label(row, $"[错误: {assetData.defName} 加载失败]");
                    drawY += rowHeight;
                    continue;
                }

                // 1. 物品图标
                Rect iconRect = new Rect(row.x, row.y + (rowHeight - iconSize) / 2f, iconSize, iconSize);
                Widgets.ThingIcon(iconRect, tempThing);

                // 2. 物品标签 (名称和数量)
                Rect labelRect = new Rect(iconRect.xMax + 5f, row.y, row.width - iconSize - 5f - 60f, rowHeight);
                Text.Anchor = TextAnchor.MiddleLeft;

                // ⭐ 优化 2: 修复名称重复问题
                string baseLabel = tempThing.def.LabelCap;
                string label = assetData.stackCount > 1
                    ? $"{baseLabel} x{assetData.stackCount}"
                    : baseLabel;

                Widgets.Label(labelRect, label);
                Text.Anchor = TextAnchor.UpperLeft;

                // ⭐ 修正 3: 移除互动按钮 (丢弃/出售)
                // 如果需要，未来可以添加一个“请求交易”的按钮。

                // 4. 工具提示
                TooltipHandler.TipRegion(row, () => tempThing.DescriptionDetailed, assetData.GetHashCode());

                // 销毁临时物品，防止内存泄漏（重要！）
                tempThing.Destroy();

                drawY += rowHeight;
            }

            Widgets.EndScrollView();
        }


        /// <summary>
        /// 绘制通知面板：经济活动日志
        /// </summary>
        private void DrawEconomicLogPanel(Rect rect, Pawn pawn, CES_PawnEconomyData data)
        {
            float lineHeight = 20f;
            float curY = rect.yMin;

            // ===== 标题 =====
            Widgets.Label(rect.TopPartPixels(lineHeight), "交易日志");
            curY += lineHeight + 5f;

            // --- 分隔线 ---
            Widgets.DrawLineHorizontal(rect.x, curY, rect.width);
            curY += 5f;

            // ===== ScrollView 准备 =====
            Rect scrollRect = new Rect(
                rect.x,
                curY,
                rect.width,
                rect.yMax - curY
            );

            // ⭐ 获取日志
            // 仅显示最近 50 条记录
            var history = data.economicHistory
                .Take(50)
                .ToList();

            if (!history.Any())
            {
                Widgets.Label(scrollRect.ContractedBy(5f), "今日无个人经济活动记录。");
                return;
            }

            // 1. 计算 viewHeight
            viewHeight = history.Count * lineHeight + 10f; // 复用左侧的 viewHeight 变量，或者定义一个新的

            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, viewHeight);
            

            Widgets.BeginScrollView(scrollRect, ref scrollPosLog, viewRect);

            // ===== 真正绘制行 =====
            float drawY = 0f;

            foreach (var logEntry in history)
            {
                Rect row = new Rect(0f, drawY, viewRect.width, lineHeight);
                
                // 2. 消息
                string fullMessage = $"{logEntry.message}";

                // 3. 颜色和绘制
                GUI.color = logEntry.color;
                Widgets.Label(row, fullMessage);
                GUI.color = Color.white; // 恢复颜色

                // 4. 工具提示 (可选: 详细信息)
                // TooltipHandler.TipRegion(row, logEntry.message); 
                drawY += lineHeight;
            }

            Widgets.EndScrollView();
        }
    }
}