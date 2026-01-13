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
        
        //
        public ThingFilter tradeThingFilter;

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
            
            
            Scribe_Deep.Look(ref tradeThingFilter, "tradeThingFilter");

            if (tradeThingFilter == null)
            {
                tradeThingFilter = new ThingFilter();
            }

            // ⭐ 核心修复：确保 RootNode 存在
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                tradeThingFilter.SetAllowAll(null);
            }
        }

        public CES_PawnEconomyData TryGetEconomyData(Pawn pawn)
        {
            if (pawn == null) return null;

            string id = pawn.GetUniqueLoadID();
            savedData.TryGetValue(id, out var data);
            
            //如果存在该经济数据，但是处于冻结状态时，依然返回空
            if (data == null) return null;
            if (!data.active) return null;
            
            return data;
        }
        
        //创建或者开启经济系统
        public CES_PawnEconomyData CreateOrEnableEconomyDataFor(Pawn pawn)
        {
            if (pawn == null) return null;

            string id = pawn.GetUniqueLoadID();
            if (!savedData.TryGetValue(id, out var data))
            {
                data = new CES_PawnEconomyData();
                savedData[id] = data;
            }

            data.active = true;
            return data;
        }
        //冻结经济系统(禁用但不删除)
        public void RemoveEconomyDataFor(Pawn pawn)
        {
            if (pawn == null) return;

            string id = pawn.GetUniqueLoadID();
            if (!savedData.TryGetValue(id, out var data)) return;

            data.active = false;
        }
        //删除经济系统
        public void PurgeEconomyDataFor(Pawn pawn)
        {
            if (pawn == null) return;
            savedData.Remove(pawn.GetUniqueLoadID());
        }

        public bool HasEconomyData(Pawn pawn)
        {
            if (pawn == null) return false;

            if ( pawn.TryGetEconomyData()!=null)
            {
                return true;
            }
            return false;

           
        }
        
        public bool ShouldHaveEconomyData(Pawn pawn)
        {
            if (pawn == null) return false;
            if (pawn.Faction != Faction.OfPlayer) return false;
            if (pawn.IsGhoul) return false;
            if (pawn.IsSlave) return false;
            
            // ⭐ 核心：囚犯 & 奴隶不应该拥有经济系统
            if (pawn.guest != null)
            {
                if (pawn.guest.IsPrisoner) return false;
                if (pawn.guest.IsSlave) return false;
            }

            return pawn.IsColonist;
            // 以后要支持囚犯？改这里就行
        }
        
        public override void LoadedGame()
        {
            base.LoadedGame();
            foreach (var pawn in PawnsFinder.AllMaps_FreeColonists)
            {
                if (pawn.TryGetEconomyData() == null)
                {
                    pawn.EnsureEconomyData();
                }
            }

            Log.Warning("[CES] 现有殖民者经济数据迁移完成。");
        }

    }
}