


using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

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
        
        //记录玩家要买的物品。
        private PrivateItemData pendingBuyItem;

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
            size = new Vector2(800f, 400f);
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
            DrawVirtualAssetPanel(rightRect, pawn, data); // ⭐ 右侧是资产
        }

        /// <summary>
        /// 绘制左侧面板：钱包、欠薪和工资构成
        /// </summary>
        private void DrawEconomicPanel(Rect rect, Pawn pawn, CES_PawnEconomyData data)
        {
            float lineHeight = 28f;
            float lineWidth = 100f;
            float curY = rect.yMin;
            float curX = rect.xMin;

            // ===== 标题 =====
            Widgets.Label(rect.TopPartPixels(lineHeight), "经济");
            curY += lineHeight + 10f;

            // ===== 顶部数值 =====
            Widgets.Label(new Rect(rect.x, curY, rect.width, lineHeight),
                $"钱包:{data.virtualWallet:F0}");
            curY += lineHeight;

            Widgets.Label(new Rect(rect.x, curY, rect.width, lineHeight),
                $"欠薪:{data.unpaidWage:F0}");
            curY += lineHeight;

            float Correction = CES_EconomyUtility.GetCorrection(Find.AnyPlayerHomeMap);
            Widgets.Label(new Rect(rect.x, curY, rect.width, lineHeight),
                $"工资系数:{Correction:F2}");
            curY += lineHeight;

            float totalWage = data.CalculatePendingWage(data.dailyWorkPreview);
            Widgets.Label(new Rect(rect.x, curY, rect.width, lineHeight),
                $"工资:{totalWage:F0}");
            curY += lineHeight + 16f;

            curX += lineWidth;
            float Profit = data.Profit;
            //如果利润为正，绿色，为负，红色
            Color color = Profit > 0 ? Color.green : Color.red;
            Widgets.Label(new Rect(curX, rect.y, rect.width, lineHeight),
                $"利润:{Profit.ToString().Colorize(color):F0)}");


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
            
            List<WorkWagePreview> previews = data.dailyWorkPreview;
            
            viewHeight = previews.Count() * rowHeight + 10f;
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, viewHeight);

            Widgets.BeginScrollView(scrollRect, ref scrollPos, viewRect);

            float drawY = 0f;

            int index = 0;
            foreach (var p in previews)
            {
                Rect row = new Rect(0f, drawY, viewRect.width, rowHeight);
                //按照优先级添加边框线
                Color outline=Color.white;
                if (p.priority == 1)
                    outline = new Color(1f, 1f, 0, 1f);   // 金色
                else if (p.priority == 2)
                    outline = new Color(1f, 1f, 1f, 1f);  // 银灰
                // ⭐ 斑马纹背景
                Color bg=Color.black;
                switch (WageUtility.GetSkillFactor(p.skillLevel).Item1)
                {
                    case "见习":
                        bg =Color.gray;;
                        break;
                    case "入门":
                        bg = Color.white;
                        break;
                    case "标准":
                        bg = Color.green;
                        break;
                    case "熟练":
                        bg =Color.cyan;
                        break;
                    case "专家":
                        bg = Color.magenta;
                        break;
                    case "大师":
                        bg =Color.yellow; 
                        break;
                }
                Widgets.DrawBoxSolidWithOutline(row, bg,outline);
                
                string label =p.workType.labelShort ?? p.workType.label;

                // ⭐ 优先级标识
                if (p.priority == 1)
                    label = ("★ " + label).Colorize(Color.white);
                else if (p.priority >= 4)
                    label = label.Colorize(Color.gray);

                // 左：岗位
                Widgets.Label(new Rect(0f, drawY, row.width * 0.3f, rowHeight), label);
                
                // 中：技能 + 倍率
                Widgets.Label(
                    new Rect(row.width * 0.32f, drawY, row.width * 0.3f, rowHeight),
                    $"Lv{p.skillLevel}|{WageUtility.GetSkillFactor(p.skillLevel).Item2:F2}"
                );

                // 右：工资
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(
                    new Rect(row.width * 0.6f, drawY, row.width * 0.38f, rowHeight),
                    $"¥{data.GetWage(p.workType,data.dailyWorkPreview):F0}"
                );
                Text.Anchor = TextAnchor.UpperLeft;

                TooltipHandler.TipRegion(row,
                    $"{p.workType.label}\n技能等级：{p.skillLevel}\n基础岗位价：{p.basePrice:F0}\n优先级：{p.priority}");

                drawY += rowHeight+6f;
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
            const float Padding = 5f;
            const float RowVerticalSpacing = 2f; // 条目间的垂直间距

            // ===== 标题 =====
            Widgets.Label(rect.TopPartPixels(lineHeight), "资产");
            curY += lineHeight + Padding;

            var assets = data.privateOwnedAssets;

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
            // 调整行高以适应图标大小、上下内边距和间距
            float contentHeight = iconSize + (2 * Padding);
            float rowHeight = contentHeight + RowVerticalSpacing;

            // 1. 计算 viewHeightVirtual
            viewHeightVirtual = assets.Count * rowHeight + Padding;

            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, viewHeightVirtual);

            Widgets.BeginScrollView(scrollRect, ref scrollPosVirtual, viewRect);

            // **定义边框和背景颜色**
            Color backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.5f); // 柔和的深灰色 (半透明)
            Color borderColor = new Color(0.25f, 0.25f, 0.25f, 1f); // 较深的灰色 (不透明)
            const int BorderThickness = 1;

            // ===== 真正绘制行 =====
            float drawY = 0f;
            float actualDrawWidth = viewRect.width;

            foreach (PrivateItemData assetData in assets)
            {
                // 1. 完整行矩形 (包含边框和间距，但不包含 RowVerticalSpacing)
                Rect fullRowRect = new Rect(0f, drawY, viewRect.width, contentHeight);

                // 尝试重建一个临时的 Thing 实例用于图标和工具提示
                Thing tempThing = assetData.RecreateThing();

                if (tempThing == null)
                {
                    GUI.color = Color.red;
                    Widgets.Label(fullRowRect.ContractedBy(Padding), $"[错误: {assetData.defName} 加载失败]");
                    GUI.color = Color.white;
                    drawY += rowHeight;
                    continue;
                }

                // 1. **调用抽象方法绘制背景和边框，并获取下一行 Y**
                float currentY = drawY; // 保存当前 Y
                drawY = DrawBoxedRowBackground(
                    new Rect(0f, 0f, actualDrawWidth, 0f),
                    currentY,
                    contentHeight,
                    RowVerticalSpacing
                );


                // --- 绘制背景和边框 ---
                GUI.color = backgroundColor;
                Widgets.DrawRectFast(fullRowRect, backgroundColor);

                GUI.color = borderColor;
                Widgets.DrawBox(fullRowRect, BorderThickness);
                GUI.color = Color.white; // 恢复颜色

                // 2. 绘制内容所需的矩形 (收缩，在边框内)
                Rect contentRect = fullRowRect.ContractedBy(Padding);

                // 3. 物品图标
                Rect iconRect = new Rect(
                    contentRect.x,
                    contentRect.y + (contentRect.height - iconSize) / 2f,
                    iconSize,
                    iconSize
                );
                Widgets.ThingIcon(iconRect, tempThing.def);

                // 4. 物品标签 (名称和数量)
                float buttonWidth = 56f;
                float priceWidth = 70f;

                Rect labelRect = new Rect(
                    iconRect.xMax + Padding,
                    contentRect.y,
                    contentRect.width - iconSize - Padding - buttonWidth - priceWidth,
                    contentRect.height
                );

                Rect priceRect = new Rect(
                    labelRect.xMax,
                    contentRect.y,
                    priceWidth,
                    contentRect.height
                );

                Rect buttonRect = new Rect(
                    priceRect.xMax + Padding,
                    contentRect.y + 4f,
                    buttonWidth,
                    contentRect.height - 8f
                );

                Text.Anchor = TextAnchor.MiddleLeft;

                string baseLabel = tempThing.def.LabelCap;
                string label = assetData.stackCount > 1
                    ? $"{baseLabel} x{assetData.stackCount}"
                    : baseLabel;

                Widgets.Label(labelRect, label);
                Text.Anchor = TextAnchor.UpperLeft;
                
                //得到售价（已包含每日价格变化）
                float price = assetData.Price;
                float priceChange = assetData.priceChange;
                String changeStr = priceChange <= 0 ? "折扣:".Colorize(Color.green) : "溢价:".Colorize(Color.red);
                
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(priceRect, $"¥{price:F0}".Colorize(priceChange<0?Color.green:Color.red));
                Text.Anchor = TextAnchor.UpperLeft;
                
                if (Widgets.ButtonText(buttonRect, "购买"))
                {
                    //⭐因为ITab不允许在结束前修改UI的数据结构，
                    //我们先记录
                    pendingBuyItem = assetData;
                    
                }

                // 5. 工具提示
                TooltipHandler.TipRegion(fullRowRect, () => tempThing.DescriptionDetailed, assetData.GetHashCode());
                TooltipHandler.TipRegion(
                    priceRect,
                    $"{changeStr}:{priceChange*100:F0}%"
                );
                tempThing.Destroy();
                // drawY 已经在 DrawBoxedRowBackground 中递增，不需要再手动增加
            }

            Widgets.EndScrollView();
            
            // ⭐ UI 绘制完成后再处理交易
            if (pendingBuyItem != null)
            {
                ITabUtility.TryBuyPrivateItem(pawn, pendingBuyItem);
                pendingBuyItem = null;
            }
        }


        /// <summary>
        /// 绘制通知面板：经济活动日志
        /// </summary>
        private void DrawEconomicLogPanel(Rect rect, Pawn pawn, CES_PawnEconomyData data)
        {
            // ... (标题和 ScrollRect 准备部分不变) ...

            const float Padding = 5f;
            const float TitleHeight = 20f;


            float curY = rect.yMin;

            // ===== 标题 =====
            Widgets.Label(rect.TopPartPixels(TitleHeight), "交易日志");
            curY += TitleHeight + Padding;

            // --- 分隔线 ---
            Widgets.DrawLineHorizontal(rect.x, curY, rect.width);
            curY += Padding;

            // ===== ScrollView 准备 =====
            Rect scrollRect = new Rect(
                rect.x,
                curY,
                rect.width,
                rect.yMax - curY
            );

            // ⭐ 获取日志
            var history = data.economicHistory
                .Take(50)
                .ToList();

            if (!history.Any())
            {
                Widgets.Label(scrollRect.ContractedBy(Padding), "今日无个人经济活动记录。");
                return;
            }

            // 1. 动态计算 viewHeight
            float dynamicViewHeight = 0f;
            float drawWidth = scrollRect.width - 16f - (2 * Padding);

            // **定义日志条目间的垂直间距**
            const float RowVerticalSpacing = 4f;

            // **第一次循环：计算总高度**
            foreach (var logEntry in history)
            {
                string fullMessage = $"{logEntry.message}";
                float textHeight = Text.CalcHeight(fullMessage, drawWidth);
                // 总高度 = 文本高度 + 顶部/底部内边距 (2 * Padding) + 条目间距
                dynamicViewHeight += textHeight + (2 * Padding) + RowVerticalSpacing;
            }

            // viewRect 的高度使用动态计算的总高度
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, dynamicViewHeight);

            // **重要：开始滚动视图**
            Widgets.BeginScrollView(scrollRect, ref scrollPosLog, viewRect);

            float drawY = 0f;
            float actualDrawWidth = viewRect.width;

            foreach (var logEntry in history)
            {
                string fullMessage = $"{logEntry.message}";
                // 确保 CalcHeight 使用的是实际绘制文本的宽度
                float drawTextWidth = actualDrawWidth - (2 * Padding);
                float textHeight = Text.CalcHeight(fullMessage, drawTextWidth);

                // 1. 计算完整内容高度 (文本 + 上下内边距)
                float contentHeight = textHeight + (2 * Padding);

                // 2. **调用抽象方法绘制背景和边框**
                // **!!! 关键步骤: 绘制并更新 drawY !!!**
                drawY = DrawBoxedRowBackground(
                    new Rect(0f, 0f, actualDrawWidth, 0f), // 传入宽度信息
                    drawY,
                    contentHeight,
                    RowVerticalSpacing
                );

                // 3. 文本绘制矩形 (在 DrawBoxedRowBackground 之后，绘制在边框内)
                // DrawBoxedRowBackground 返回的是下一行的 Y 坐标，所以我们需要回退一下 Y 轴

                // 计算文本绘制的起始Y坐标（当前drawY - (contentHeight + spacing)）
                float textStartX = drawY - (contentHeight + RowVerticalSpacing);

                Rect textRect = new Rect(
                    Padding,
                    textStartX + Padding,
                    drawTextWidth,
                    textHeight
                );

                // 4. 绘制文本
                Widgets.Label(textRect, fullMessage);

                // 5. 工具提示
                // 绑定到绘制背景时的矩形，即：
                Rect tooltipRect = new Rect(0f, textStartX, actualDrawWidth, contentHeight);
                TooltipHandler.TipRegion(tooltipRect, logEntry.message);
                
            }

            Widgets.EndScrollView();
        }

        // 假设这个方法放在您的 ITab 派生类内部
        private static float DrawBoxedRowBackground(
            Rect rect,
            float currentY,
            float contentHeight,
            float verticalSpacing = 2f)
        {
            // 定义颜色和边框
            Color backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.5f);
            Color borderColor = new Color(0.25f, 0.25f, 0.25f, 1f);
            const int BorderThickness = 1;

            // 1. 定义绘制的完整矩形
            Rect fullRowRect = new Rect(
                rect.x,
                currentY,
                rect.width,
                contentHeight
            );

            // 2. 绘制背景
            GUI.color = backgroundColor;
            Widgets.DrawRectFast(fullRowRect, backgroundColor);

            // 3. 绘制边框
            GUI.color = borderColor;
            Widgets.DrawBox(fullRowRect, BorderThickness);

            GUI.color = Color.white; // 恢复颜色

            // 4. 返回下一行的起始 Y 坐标 (包含内容高度和行间距)
            return currentY + contentHeight + verticalSpacing;
        }
    }
}