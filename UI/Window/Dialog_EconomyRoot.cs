using UnityEngine;
using Verse;

namespace EconomicSystem
{
    // 经济系统主面板（骨架）
    public class Dialog_EconomyRoot : Window
    {
        private enum EconomyTab
        {
            Overview,
            Market,
            Pawns,
            ForeignTrade,
            Policy
        }

        private EconomyTab currentTab = EconomyTab.Overview;
        private CES_EconomyGameComponent econ;

        // ⭐ Market 模块需要的 ThingFilter
        private ThingFilter tradeThingFilter;
        private ThingFilterUI.UIState tradeFilterUIState;

        public override Vector2 InitialSize => new Vector2(1100f, 720f);

        public Dialog_EconomyRoot()
        {
            doCloseX = true;
            draggable = true;
            absorbInputAroundWindow = true;

            econ = Current.Game.GetComponent<CES_EconomyGameComponent>();

            // === 集成之前写好的可交易 ThingFilter ===
            if (econ != null)
            {
                if (econ.tradeThingFilter == null)
                {
                    econ.tradeThingFilter = new ThingFilter();
                    econ.tradeThingFilter.SetAllowAll(null);
                    econ.tradeThingFilter.ResolveReferences();
                }

                tradeThingFilter = econ.tradeThingFilter;
                tradeFilterUIState = new ThingFilterUI.UIState();
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            float tabHeight = 32f;
            Rect tabRect = new Rect(inRect.x, inRect.y, inRect.width, tabHeight);
            DrawTabs(tabRect);

            Rect contentRect = new Rect(
                inRect.x,
                inRect.y + tabHeight + 6f,
                inRect.width,
                inRect.height - tabHeight - 6f
            );

            switch (currentTab)
            {
                case EconomyTab.Overview:
                    DrawOverview(contentRect);
                    break;
                case EconomyTab.Market:
                    DrawMarket(contentRect);
                    break;
                case EconomyTab.Pawns:
                    DrawPawns(contentRect);
                    break;
                case EconomyTab.ForeignTrade:
                    DrawForeignTrade(contentRect);
                    break;
                case EconomyTab.Policy:
                    DrawPolicy(contentRect);
                    break;
            }
        }

        private void DrawTabs(Rect rect)
        {
            float x = rect.x;
            foreach (EconomyTab tab in System.Enum.GetValues(typeof(EconomyTab)))
            {
                Rect buttonRect = new Rect(x, rect.y, 120f, rect.height);
                if (Widgets.ButtonText(buttonRect, tab.ToString()))
                {
                    currentTab = tab;
                }
                x += 124f;
            }
        }

        private void DrawOverview(Rect rect)
        {
            Widgets.Label(rect, "[CES] Overview module (WIP)");
        }

        // ⭐ 核心：Market 模块中直接嵌入 ThingFilter
        private void DrawMarket(Rect rect)
        {
            if (tradeThingFilter == null)
            {
                Widgets.Label(rect, "Trade filter not initialized.");
                return;
            }

            // 左侧留给 ThingFilter
            Rect filterRect = new Rect(rect.x, rect.y, 420f, rect.height);

            ThingFilterUI.DoThingFilterConfigWindow(
                filterRect,
                tradeFilterUIState,
                tradeThingFilter,
                null,
                1,
                null,
                null,
                false,
                false,
                false,
                null,
                Find.CurrentMap
            );

            // 右侧未来放“市场物品列表”
            Rect listRect = new Rect(
                rect.x + 430f,
                rect.y,
                rect.width - 430f,
                rect.height
            );

            Widgets.DrawMenuSection(listRect);
            Widgets.Label(listRect.ContractedBy(10f), "[CES] Market items list (next step)");
        }

        private void DrawPawns(Rect rect)
        {
            Widgets.Label(rect, "[CES] Pawns economy module (WIP)");
        }

        private void DrawForeignTrade(Rect rect)
        {
            Widgets.Label(rect, "[CES] Foreign trade module (WIP)");
        }

        private void DrawPolicy(Rect rect)
        {
            Widgets.Label(rect, "[CES] Policy & rules module (WIP)");
        }
    }
}
