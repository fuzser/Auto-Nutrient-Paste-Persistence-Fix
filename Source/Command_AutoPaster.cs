// ======================================================================
// 文件名: Command_AutoPaster.cs
// 模块: 自定义 UI 控件
// 职责: 定义一个自定义的按钮 (Gizmo) 类，用于在营养膏机的界面上
//       显示一个带有自定义文字和颜色变化的开关按钮。
// ======================================================================

// 引入所有必要的命名空间
using UnityEngine; // 用于使用 Rect, Vector2, GUI, Color 等 Unity 引擎的 UI 功能
using Verse;      // RimWorld 的基础类和工具，包括 Gizmo, Widgets, Text 等

namespace AutoNutrientPaste
{
    /// <summary>
    /// 我们自定义的按钮类。
    /// 它继承自 RimWorld 内置的 Command_Toggle 类，这样做的好处是：
    /// 1. 自动获得了开关按钮的基础功能（点击切换状态、声音反馈等）。
    /// 2. 我们可以只重写 (override) 我们关心的方法 (GizmoOnGUI)，而不需要从头造轮子。
    /// </summary>
    public class Command_AutoPaster : Command_Toggle
    {
        /// <summary>
        /// 一个公共字段，用于从外部（即 Harmony 补丁中创建按钮实例的地方）
        /// 传入我们希望在按钮下方显示的自定义文字，例如 "Auto" 或 "Cont."。
        /// </summary>
        public string ButtonText = "";

        /// <summary>
        /// 重写此方法来完全控制按钮的绘制过程。
        /// RimWorld 的 UI 系统在每一帧都会调用这个方法来绘制按钮。
        /// </summary>
        /// <param name="topLeft">按钮左上角在屏幕上的坐标。</param>
        /// <param name="maxWidth">按钮可用的最大宽度。</param>
        /// <param name="parms">其他渲染参数，例如是否被高亮等。</param>
        /// <returns>一个 GizmoResult 对象，包含了本次绘制和交互的结果（例如按钮是否被点击）。</param>
        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            // --- 步骤 1: 执行基类（父类）的绘制 ---
            // 调用 base.GizmoOnGUI(...) 会先让 Command_Toggle 类把自己绘制出来。
            // 这包括了按钮的标准背景、我们设置的主图标（营养膏图标），以及处理点击事件。
            // 我们必须先做这一步，并保存它的返回值。
            GizmoResult result = base.GizmoOnGUI(topLeft, maxWidth, parms);

            // --- 步骤 2: 计算自定义文字的绘制区域 ---
            // 创建一个与按钮本身大小和位置完全相同的矩形 (Rect)。
            Rect textRect = new Rect(topLeft.x, topLeft.y, this.GetWidth(maxWidth), Gizmo.Height);
            // 对这个矩形进行微调，让它位于按钮的下半部分，为文字留出空间。
            textRect.y += textRect.height * 0.55f; // Y 坐标向下偏移，越过图标
            textRect.height *= 0.45f;              // 高度缩小，只占据下半部分

            // --- 步骤 3: 设置 GUI 绘制状态 ---
            // 在绘制任何东西之前，我们需要设置好“画笔”的属性。
            Text.Font = GameFont.Small;           // 设置字体大小为“小号”
            Text.Anchor = TextAnchor.UpperCenter; // 设置文字在 textRect 区域内顶部居中对齐

            // 根据按钮的当前激活状态，改变文字的颜色
            if (this.isActive()) // this.isActive() 是从 Command_Toggle 继承来的方法，它会调用我们设置的 isActive 委托
            {
                // 如果是激活状态，将后续所有绘制操作的颜色设置为亮绿色。
                GUI.color = Color.green;
            }
            // 如果是非激活状态，我们不设置颜色，让它保持默认的白色。

            // --- 步骤 4: 绘制文字 ---
            // 使用 Widgets.Label 方法，在指定的 textRect 区域内，绘制我们从外部传入的 ButtonText。
            Widgets.Label(textRect, ButtonText);

            // --- 步骤 5: 恢复 GUI 状态 (非常重要！) ---
            // 在完成所有自定义绘制后，我们必须将所有修改过的 GUI 设置恢复到默认值。
            // 如果不这样做，我们设置的绿色或居中对齐，可能会“污染”到游戏中后续其他 UI 元素的绘制，导致界面错乱。
            Text.Font = GameFont.Small;           // 恢复默认字体大小 (虽然我们没改，但这是个好习惯)
            Text.Anchor = TextAnchor.UpperLeft;   // 恢复默认的左上角对齐
            GUI.color = Color.white;              // 恢复默认的白色

            // --- 步骤 6: 返回结果 ---
            // 将我们从基类方法中获取的结果返回。
            return result;
        }
    }
}