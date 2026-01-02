using RimWorld;
using Verse;
using Verse.Sound;

namespace EconomicSystem
{
    public class ITabUtility
    {
        public static void TryBuyPrivateItem(Pawn pawn, PrivateItemData asset)
        {
            
            Map map = pawn.Map;
            if (map == null) return;
            
            var economy = map.GetComponent<MapComponent_ColonyEconomy>();
            
            CES_PawnEconomyData data = pawn.GetEconomyData();
            if (data == null)return;
            
            int price = asset.Price;

            // 殖民地资金不足
            if (economy.GetTotalSilver()<price)
            {
                Messages.Message("殖民地资金不足。", MessageTypeDefOf.RejectInput);
                return;
            }

            // 扣钱
            if (economy.TryConsumeSilver(price))
            {
                //殖民者加钱
                data.virtualWallet += price;
            }

            // 生成物品
            Thing thing = asset.RecreateThing();
            GenPlace.TryPlaceThing(thing, pawn.Position, map, ThingPlaceMode.Near);

            // 移除私人物品
            data.privateOwnedAssets.Remove(asset);

            // 写日志
            data.economicHistory.Add(EconomicLogEntry.NewLog($"以{price:F0}白银将{thing.LabelCap}卖给了玩家"));
            

            SoundDefOf.ExecuteTrade.PlayOneShotOnCamera();
        }

    }
}