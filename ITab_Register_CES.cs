using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using System.Linq;
using System.Reflection;

// 注意：我们必须使用 CashRegister 的命名空间和类型，否则将无法继承 ITab_Register
// 命名空间保持不变，以便编译器找到 ITab_Register
namespace CashRegister
{
    // 定义一个我们自己的 ITab，继承自 CashRegister Mod 的 ITab_Register 抽象类
    public class ITab_Register_CES : ITab_Register
    {
        // 反射信息（在静态构造函数中初始化）
        private static Type BuildingCashRegisterType;
        private static PropertyInfo RadiusProperty;
        private static PropertyInfo IsActiveProperty;
        private static PropertyInfo CompAssignableToPawnProperty;
        private static FieldInfo ShiftsField;

        // 静态构造函数用于初始化反射
        static ITab_Register_CES()
        {
            // 假设 CashRegister Mod 的类型在某个加载的 Assembly 中
            BuildingCashRegisterType = GenTypes.GetTypeInAnyAssembly("CashRegister.Building_CashRegister");

            if (BuildingCashRegisterType != null)
            {
                // 获取公共属性 Radius (get)
                RadiusProperty = BuildingCashRegisterType.GetProperty("Radius", BindingFlags.Instance | BindingFlags.Public);
                
                // 获取公共属性 IsActive (get)
                IsActiveProperty = BuildingCashRegisterType.GetProperty("IsActive", BindingFlags.Instance | BindingFlags.Public);

                // 获取公共属性 CompAssignableToPawn (get)
                CompAssignableToPawnProperty = BuildingCashRegisterType.GetProperty("CompAssignableToPawn", BindingFlags.Instance | BindingFlags.Public);
                
                // 获取公共字段 shifts (public List<Shift> shifts)
                ShiftsField = BuildingCashRegisterType.GetField("shifts", BindingFlags.Instance | BindingFlags.Public);
            }
        }

        // --- 构造函数 ---
        public ITab_Register_CES() : base(new Vector2(400f, 250f))
        {
            // 设置标签页标题
            this.labelKey = "CES_TabTitle"; // 稍后在 XML/语言文件中定义
            // 默认尺寸是继承自基类的构造函数参数
        }

        // --- 继承自 ITab_Register 的抽象方法实现 ---
        public override bool CanAssignToShift(Pawn pawn)
        {
            // 我们的 ITab 不涉及 Pawn 分配逻辑，返回 false 即可
            return false;
        }

        // --- 核心 UI 绘制逻辑 ---
        protected override void FillTab()
        {
            if (BuildingCashRegisterType == null)
            {
                Widgets.Label(new Rect(10f, 10f, 380f, 30f), "CES_CR_TypeNotFound".Translate());
                return;
            }

            // 获取当前的 Building_CashRegister 实例（SelThing 在 ITab_Register 中转换为 Register 属性）
            object registerInstance = base.SelThing; // 直接使用 SelThing 或 Register 属性（已在基类中实现）
            if (registerInstance == null) return;
            
            float curY = 10f;
            float lineHeight = 30f;
            Rect rect = new Rect(10f, curY, this.size.x - 20f, lineHeight);

            // 1. 标题
            Widgets.Label(rect, "CES_CR_TestPanelTitle".Translate());
            curY += lineHeight;
            
            // 绘制分隔线
            Widgets.DrawLineHorizontal(10f, curY, this.size.x - 20f);
            curY += 5f;

            // 2. 检查反射结果
            if (RadiusProperty != null)
            {
                float radius = (float)RadiusProperty.GetValue(registerInstance);
                Widgets.Label(new Rect(10f, curY, this.size.x - 20f, lineHeight), "CES_CR_Radius".Translate() + ": " + radius.ToString("F0"));
                curY += lineHeight;
            }

            if (IsActiveProperty != null)
            {
                bool isActive = (bool)IsActiveProperty.GetValue(registerInstance);
                Widgets.Label(new Rect(10f, curY, this.size.x - 20f, lineHeight), "CES_CR_IsActive".Translate() + ": " + (isActive ? "Yes" : "No"));
                curY += lineHeight;
            }

            // 3. 检查 Shifts 数量
            if (ShiftsField != null)
            {
                // shifts 是 List<Shift> 类型，需要强制转换
                object shiftsRaw = ShiftsField.GetValue(registerInstance);
                int shiftsCount = 0;
                if (shiftsRaw is IEnumerable<object> shiftsList)
                {
                    shiftsCount = shiftsList.Count();
                }
                Widgets.Label(new Rect(10f, curY, this.size.x - 20f, lineHeight), "CES_CR_ShiftsCount".Translate() + ": " + shiftsCount);
                curY += lineHeight;
            }
            
            // 4. 提醒（如果需要）
            curY += 10f;
            Widgets.Label(new Rect(10f, curY, this.size.x - 20f, 60f), "CES_CR_Reminder".Translate());
        }
    }
}