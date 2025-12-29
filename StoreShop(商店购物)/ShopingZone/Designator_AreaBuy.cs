using RimWorld;
using Verse;
using UnityEngine;
using System;
using System.Linq;

namespace EconomicSystem
{
    public class Designator_AreaBuy : Designator_AreaAllowed
    {
        public Designator_AreaBuy() : base(DesignateMode.Add)
        {
            this.defaultLabel = "购物区";
            this.defaultDesc = "划定一个购买区域。";
            this.icon = ContentFinder<Texture2D>.Get("UI/Designators/BuyArea", true);
            this.soundDragSustain = SoundDefOf.Designate_DragStandard;
            this.soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
            this.soundSucceeded = SoundDefOf.Designate_ZoneAdd;
            this.hotKey = KeyBindingDefOf.Misc9;
            this.tutorTag = "BuyAreaDesignator";
        }

        public override void ProcessInput(Event ev)
        {
            if (!this.CheckCanInteract())
                return;

            var buyArea = Map.areaManager.AllAreas.OfType<Area_Buy>().FirstOrDefault();
            if (buyArea == null)
            {
                buyArea = new Area_Buy(Map.areaManager);
                buyArea.RenamableLabel = "购物区";
                Map.areaManager.AllAreas.Add(buyArea);
            }

            Designator_AreaAllowed.selectedArea = buyArea;

            // 不调用 base.ProcessInput，直接设为当前设计器
            Find.DesignatorManager.Select(this);
        }


        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            if (!c.InBounds(this.Map))
                return false;
            return true;
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            if (Designator_AreaAllowed.selectedArea != null)
            {
                Designator_AreaAllowed.selectedArea[c] = true;
            }
        }
    }
}