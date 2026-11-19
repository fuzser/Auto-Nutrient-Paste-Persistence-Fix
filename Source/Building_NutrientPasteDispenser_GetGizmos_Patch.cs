// ======================================================================
// 文件名: Building_NutrientPasteDispenser_GetGizmos_Patch.cs
// 模块: Harmony 补丁模块
// 职责: 负责将自定义的控制按钮动态“注入”到原版营养膏机的UI界面上。
// ======================================================================

// 引入所有必要的命名空间
using System.Collections.Generic; // 用于使用 List<>
using HarmonyLib;                 // Harmony 库的核心功能
using RimWorld;                   // RimWorld 的核心游戏逻辑
using Verse;                      // RimWorld 的基础类和工具

namespace AutoNutrientPaste
{
    /// <summary>
    /// Harmony 补丁的启动器 (Patcher)。
    /// [StaticConstructorOnStartup] 是 RimWorld 的一个特殊属性，它告诉游戏：
    /// 当游戏启动加载所有内容时，请立即执行这个类的静态构造函数。
    /// 这是确保我们的补丁能够在游戏启动时被应用的关键。
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        /// <summary>
        /// 静态构造函数。这是本 Mod 的 C# 代码最早被执行的地方。
        /// </summary>
        static HarmonyPatches()
        {
            // 创建一个新的 Harmony 实例。
            // 构造函数中的字符串是这个修复版的唯一 Harmony ID，
            // 必须与您 Mod 的 packageId 保持一致，以避免与其他 Mod 冲突。
            var harmony = new Harmony("worldleader.autonutrientpaste");

            // 调用 PatchAll()。这个强大的方法会自动扫描当前整个程序集（即我们的 .dll 文件），
            // 寻找所有带有 [HarmonyPatch] 属性的类，并自动应用它们。
            harmony.PatchAll(typeof(HarmonyPatches).Assembly);
        }
    }

    /// <summary>
    /// 这是一个针对原版 Building_NutrientPasteDispenser 类的 GetGizmos 方法的 Harmony 补丁。
    /// [HarmonyPatch] 属性是给 Harmony 看的“指令”，告诉它要去修改哪个目标。
    /// </summary>
    [HarmonyPatch(typeof(Building_NutrientPasteDispenser), "GetGizmos")]
    public static class Building_NutrientPasteDispenser_GetGizmos_Patch
    {
        /// <summary>
        /// Postfix（后缀）补丁方法。
        /// 这个方法会在原版的 GetGizmos 方法完整执行完毕后，被自动调用。
        /// 我们可以利用它来读取和修改原方法的最终返回值。
        /// </summary>
        /// <param name="__instance">
        /// Harmony 提供的特殊参数，它代表了当前被调用 GetGizmos 方法的那个营养膏机实例 (instance)。
        /// </param>
        /// <param name="__result">
        /// Harmony 提供的特殊参数，它通过 `ref` 关键字，代表了原方法 `GetGizmos` 的返回值。
        /// 在这里，它是一个 `IEnumerable<Gizmo>`（按钮列表）。我们可以直接修改这个 `__result` 来添加或删除按钮。
        /// </param>
        public static void Postfix(Building_NutrientPasteDispenser __instance, ref IEnumerable<Gizmo> __result)
        {
            // 从我们的全局管理器中获取单例引用。
            var manager = AutoNutrientPasteManager.Current;
            // 这是一个重要的安全检查。如果在某些极端情况下管理器未能初始化，我们就直接返回，避免出错。
            if (manager == null) return;

            // 为了方便地添加新按钮，我们将原方法返回的（可能是只读的）IEnumerable 转换为一个可修改的 List。
            var gizmoList = new List<Gizmo>(__result);

            // --- 按钮 1: 智能制作 (Smart Production) ---
            var smartCommand = new Command_AutoPaster // 使用我们自定义的按钮类
            {
                // defaultLabel 是按钮的标题。我们使用 .Translate() 来调用翻译文件。
                defaultLabel = "ANP_GizmoLabel_Smart".Translate(),
                // defaultDesc 是当鼠标悬停在按钮上时，显示的详细描述文字。
                defaultDesc = "ANP_GizmoTooltip_Smart".Translate(),
                // icon 是按钮上显示的主图标。我们直接使用游戏内营养膏餐食的UI图标。
                icon = ThingDefOf.MealNutrientPaste.uiIcon,

                // isActive 是一个委托 (delegate)，它是一个“待执行”的指令。
                // 游戏每一帧都会调用这个委托来决定按钮是否应该显示为“高亮”（激活）状态。
                // 这里的逻辑是：调用管理器的方法，检查当前这个机器（__instance）的模式是否为 Smart。
                isActive = () => manager.GetProductionMode(__instance) == ProductionMode.Smart,

                // toggleAction 也是一个委托，它定义了当玩家点击这个按钮时，应该执行什么操作。
                // 这里的逻辑是：调用管理器的方法，将当前这个机器的模式设置为 Smart。
                toggleAction = () => manager.SetProductionMode(__instance, ProductionMode.Smart),

                // 这是我们自定义按钮类 Command_AutoPaster 的一个公共字段，用于在图标下方绘制文字。
                ButtonText = "Auto"
            };
            // 将创建好的按钮添加到列表中。
            gizmoList.Add(smartCommand);

            // --- 按钮 2: 连续制作 (Continuous Production) ---
            var continuousCommand = new Command_AutoPaster
            {
                defaultLabel = "ANP_GizmoLabel_Continuous".Translate(),
                defaultDesc = "ANP_GizmoTooltip_Continuous".Translate(),
                icon = ThingDefOf.MealNutrientPaste.uiIcon,

                // 这里的逻辑与智能按钮类似，只是判断和设置的模式变为了 Continuous。
                isActive = () => manager.GetProductionMode(__instance) == ProductionMode.Continuous,
                toggleAction = () => manager.SetProductionMode(__instance, ProductionMode.Continuous),

                // 为了区分，这里使用了一个更短的文字。
                ButtonText = "Cont."
            };
            gizmoList.Add(continuousCommand);

            // 最后，将我们修改过的、包含了两个新按钮的列表，重新赋值给 __result。
            // 这样，游戏最终就会在界面上绘制出这个包含了我们新按钮的列表。
            __result = gizmoList;
        }
    }
}
