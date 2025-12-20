using Verse;
using RimWorld;
using System.Linq;
using LudeonTK; // 引入 Linq 才能使用 FirstOrDefault()

namespace EconomicSystem
{
    // DebugActionGroups 的命名将决定菜单的名称 (例如：EconomicSystem Debug)
    public static class DebugActions_CES
    {
        private const string GroupName = "CES Economy Debug";

        // --- 辅助方法：获取选中的 Pawn 和经济数据 ---
        private static CES_PawnEconomyData GetSelectedPawnData(out Pawn pawn)
        {
            pawn = Find.Selector.SelectedPawns.FirstOrDefault();
            
            if (pawn == null)
            {
                Messages.Message("No colonist selected.", MessageTypeDefOf.RejectInput, false);
                return null;
            }

            CES_PawnEconomyData econ = pawn.GetEconomyData(); 
            
            if (econ == null)
            {
                Messages.Message($"Pawn {pawn.NameShortColored} has no Economic Data.", MessageTypeDefOf.RejectInput, false);
                return null;
            }
            return econ;
        }

        // --- 1. 钱包操作 ---

        [DebugAction(GroupName, "Wallet: +1000 Silver", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void AddVirtualSilver_1000()
        {
            if (GetSelectedPawnData(out Pawn pawn) is CES_PawnEconomyData econ)
            {
                econ.AddMoney(1000);
                Messages.Message($"{pawn.NameShortColored} Wallet: {econ.virtualWallet:F0}", MessageTypeDefOf.NeutralEvent, false);
            }
        }
        
        [DebugAction(GroupName, "Wallet: -500 Silver", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void SubtractVirtualSilver_500()
        {
            if (GetSelectedPawnData(out Pawn pawn) is CES_PawnEconomyData econ)
            {
                // 使用 SubtractMoney，它会处理余额不足的情况
                if (econ.SubtractMoney(500))
                {
                    Messages.Message($"{pawn.NameShortColored} Wallet: {econ.virtualWallet:F0}", MessageTypeDefOf.NeutralEvent, false);
                }
                else
                {
                    Messages.Message($"{pawn.NameShortColored} insufficient funds ({econ.virtualWallet:F0}).", MessageTypeDefOf.RejectInput, false);
                }
            }
        }
        
        // --- 2. 欠薪操作 ---

        [DebugAction(GroupName, "Unpaid: Add 500 Wage", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void AddUnpaidWage_500()
        {
            if (GetSelectedPawnData(out Pawn pawn) is CES_PawnEconomyData econ)
            {
                econ.AddUnpaid(500);
                Messages.Message($"{pawn.NameShortColored} Unpaid Wage: {econ.unpaidWage:F0}", MessageTypeDefOf.NeutralEvent, false);
            }
        }

        [DebugAction(GroupName, "Unpaid: Clear All Wage", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void ClearUnpaidWage()
        {
            if (GetSelectedPawnData(out Pawn pawn) is CES_PawnEconomyData econ)
            {
                econ.unpaidWage = 0f;
                Messages.Message($"{pawn.NameShortColored} Unpaid Wage Cleared.", MessageTypeDefOf.NeutralEvent, false);
            }
        }

        // --- 3. 工作量操作 (用于测试工资计算) ---

        [DebugAction(GroupName, "Work: Add 100 Research", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void AddResearchWork()
        {
            if (GetSelectedPawnData(out Pawn pawn) is CES_PawnEconomyData econ)
            {
                econ.AddWork(WorkTypeDefOf.Research, 100f);
                float pendingWage = econ.CalculatePendingWage();
                Messages.Message($"{pawn.NameShortColored} Research Work Added. Pending Wage: {pendingWage:F0}", MessageTypeDefOf.NeutralEvent, false);
            }
        }

        [DebugAction(GroupName, "Work: Clear All Work", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void ClearAllWork()
        {
            if (GetSelectedPawnData(out Pawn pawn) is CES_PawnEconomyData econ)
            {
                econ.ClearPendingWork();
                Messages.Message($"{pawn.NameShortColored} Pending Work Cleared.", MessageTypeDefOf.NeutralEvent, false);
            }
        }
    }
}