using System.Collections.Generic;
using RimWorld;
using Verse;

//经济游戏组件
namespace EconomicSystem
{
    /// <summary>
    /// CES 的全局经济系统组件
    /// 
    /// GameComponent 是 RimWorld 提供的
    /// “世界级别持久化对象”
    /// 
    /// 特点：
    /// - 整个存档只有一个实例
    /// - 不依赖地图
    /// - 自动参与存档 / 读档
    /// </summary>
    //GameComponent，“类似Unity中的ScriptableObject”，是真正用于保存数据的类。
    public class CES_EconomyGameComponent : GameComponent
    {
        /// <summary>
        /// key：人物ID(Pawn.ThingID)
        /// 
        /// </summary>
        private Dictionary<string, CES_ColonistEconomyData> savedData
            = new();

        public CES_EconomyGameComponent(Game game) { }

        //这是 RimWorld 存档,读档,世界初始化时自动调用的
        public override void ExposeData()
        {
            Scribe_Collections.Look(
                ref savedData,
                "CES_ColonistEconomyData",
                LookMode.Value,
                LookMode.Deep
            );
        }

        //每250tick时同步一次数据
        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 250 != 0) return;

            SyncPawnData();
        }

        //同步所有Pawn的数据
        private void SyncPawnData()
        {
            //得到所有地图上所有的殖民者
            foreach (var pawn in PawnsFinder.AllMaps_FreeColonists)
            {
                //得到该殖民者的经济数据
                var data = pawn.GetEconomyData();
                if (data == null) continue;

                //将得到的最新经济数据保存到字典中
                savedData[pawn.ThingID] = data;
            }
        }
    }
}