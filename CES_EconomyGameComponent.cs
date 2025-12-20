using System.Collections.Generic;
using RimWorld;
using Verse;

//经济游戏组件
namespace EconomicSystem
{
    /// <summary>
    /// CES 的全局经济系统组件
    /// GameComponent 是 RimWorld 提供的
    /// “世界级别持久化对象”
    /// </summary>
    public class CES_EconomyGameComponent : GameComponent
    {
        /// <summary>
        /// key：人物ID(Pawn.ThingID)
        /// 
        /// </summary>
        private Dictionary<string, CES_PawnEconomyData> savedData
            = new();

        public CES_EconomyGameComponent(Game game) { }

        //这是 RimWorld 存档,读档,世界初始化时自动调用的
        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Collections.Look(
                ref savedData,
                "CES_ColonistEconomyData",
                LookMode.Value,
                LookMode.Deep
            );

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                savedData ??= new Dictionary<string, CES_PawnEconomyData>();
            }
        }

        public CES_PawnEconomyData GetOrCreateDataFor(Pawn pawn)
        {
            if (pawn == null || !pawn.IsColonist)
                return null;

            string id = pawn.GetUniqueLoadID();

            if (!savedData.TryGetValue(id, out var data))
            {
                data = new CES_PawnEconomyData();
                savedData[id] = data;
            }

            return data;
        }

    }
}