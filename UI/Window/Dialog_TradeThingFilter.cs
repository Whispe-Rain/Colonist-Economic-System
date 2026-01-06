using UnityEngine;
using Verse;

namespace EconomicSystem
{
    public class Dialog_TradeThingFilter : Window
    {
        private ThingFilter filter;
        private ThingFilterUI.UIState uiState;

        public override Vector2 InitialSize => new Vector2(900f, 700f);

        public Dialog_TradeThingFilter(ThingFilter filter)
        {
            this.filter = filter;
            this.uiState = new ThingFilterUI.UIState();

            if (this.filter == null)
            {
                Log.Error("[CES] TradeThingFilter is null!");
                return;
            }

            // ⭐ 关键修复 1：保证基础内容
            this.filter.SetAllowAll(null);

            // ⭐ 关键修复 2：构建 RootNode（必须）
            this.filter.ResolveReferences();
            doCloseX = true;
            draggable = true;
            absorbInputAroundWindow = true;
        }


        public override void DoWindowContents(Rect inRect)
        {
            if (filter == null || filter.RootNode == null)
            {
                Widgets.Label(inRect, "Trade filter not initialized.");
                return;
            }

            ThingFilterUI.DoThingFilterConfigWindow(
                inRect,
                uiState,
                filter,
                null,
                1,
                null,
                null,
                false,
                false,
                false,
                null,
                Find.CurrentMap   // ⭐ 给 map（可选，但更稳）
            );
        }

    }
}