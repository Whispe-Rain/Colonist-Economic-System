using RimWorld;
using Verse;
using UnityEngine;
using System.Linq;

namespace EconomicSystem
{
    public class Designator_AreaBuyClear : Designator_AreaAllowed
    {
        public Designator_AreaBuyClear() : base(DesignateMode.Remove)
        {
            this.defaultLabel = "清除购物区";
            this.defaultDesc = "清除一个购买区域。";
            this.icon = ContentFinder<Texture2D>.Get("UI/Designators/BuyArea", true);
            this.soundDragSustain = SoundDefOf.Designate_DragStandard;
            this.soundSucceeded = SoundDefOf.Designate_ZoneDelete;
            this.hotKey = KeyBindingDefOf.Misc10; // 自定义快捷键
            this.tutorTag = "BuyAreaClearDesignator";
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            return c.InBounds(this.Map) 
                   && Designator_AreaAllowed.SelectedArea != null 
                   && Designator_AreaAllowed.SelectedArea[c];
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            if (Designator_AreaAllowed.SelectedArea != null)
            {
                Designator_AreaAllowed.SelectedArea[c] = false;
            }
        }

        public override void ProcessInput(Event ev)
        {
            if (!this.CheckCanInteract())
                return;

            // 找到现有购物区，或者新建一个并添加
            var buyArea = Map.areaManager.AllAreas.OfType<Area_Buy>().FirstOrDefault();
            if (buyArea == null)
            {
                buyArea = new Area_Buy(Map.areaManager);
                buyArea.RenamableLabel = "购物区";
                Map.areaManager.AllAreas.Add(buyArea);
            }

            // 设置当前操作区域为购物区
            Designator_AreaAllowed.selectedArea = buyArea;

            // 选中当前设计器，开始操作
            Find.DesignatorManager.Select(this);
        }
    }
}