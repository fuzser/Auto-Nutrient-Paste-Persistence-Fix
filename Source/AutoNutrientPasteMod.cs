// ======================================================================
// 文件名: AutoNutrientPasteMod.cs
// 模块: Mod 主类与设置
// 职责: 1. 定义并管理 Mod 的所有可配置项 (Settings)。
//       2. 负责在游戏的 Mod 设置菜单中绘制用户交互界面 (UI)。
// ======================================================================

// 引入所有必要的命名空间
using UnityEngine; // 用于使用 Rect (UI布局)
using Verse;       // RimWorld 的基础类和工具

namespace AutoNutrientPaste
{
    /// <summary>
    /// 定义了本 Mod 所有需要保存的设置项。
    /// RimWorld 的存档系统会自动处理这个类的序列化（保存/加载）。
    /// </summary>
    public class AutoNutrientPasteSettings : ModSettings
    {
        // --- 可配置的变量 ---

        /// <summary>
        /// 智能模式的生产下限：当营养膏数量低于此值时，开始生产。
        /// </summary>
        public int lowerThreshold = 30;

        /// <summary>
        /// 智能模式的生产上限：生产将持续进行，直到营养膏数量达到此值。
        /// </summary>
        public int upperThreshold = 60;

        /// <summary>
        /// 是否启用智能模式下的储料斗自动控制功能。
        /// </summary>
        public bool disableHoppers = true;

        /// <summary>
        /// RimWorld 的存档/读档系统在处理 Mod 设置时会调用此方法。
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();
            // Scribe_Values.Look 用于读写基本类型变量。
            // 它会将变量的值与 XML 文件中的一个标签关联起来。
            // 第三个参数 (e.g., 30) 是当存档中找不到该值时的默认值。
            Scribe_Values.Look(ref lowerThreshold, "lowerThreshold", 30);
            Scribe_Values.Look(ref upperThreshold, "upperThreshold", 60);
            Scribe_Values.Look(ref disableHoppers, "disableHoppers", true);
        }
    }

    /// <summary>
    /// Mod 的主类，继承自 Verse.Mod。这是 Mod 代码的入口点。
    /// 它也负责绘制 Mod 在游戏“选项”菜单中的设置窗口。
    /// </summary>
    public class AutoNutrientPasteMod : Mod
    {
        /// <summary>
        /// 一个静态引用，方便在 Mod 的其他地方快速、安全地访问到当前的设置实例。
        /// </summary>
        public static AutoNutrientPasteSettings settings;

        // 为可编辑的数字输入框准备的字符串“缓冲区”。
        // Widgets.TextFieldNumeric 控件需要一个临时的字符串来存储用户的文本输入，
        // 然后再将其解析为数字。这是该控件工作所必需的。
        private string lowerThresholdBuffer;
        private string upperThresholdBuffer;

        /// <summary>
        /// Mod 的构造函数。在游戏启动时，RimWorld 会创建这个类的实例。
        /// </summary>
        public AutoNutrientPasteMod(ModContentPack content) : base(content)
        {
            // GetSettings<T>() 是 Mod 基类提供的方法，它会加载或创建一个设置类的实例。
            settings = GetSettings<AutoNutrientPasteSettings>();
            // 根据加载的设置值，初始化字符串缓冲区，确保 UI 显示的初始值是正确的。
            lowerThresholdBuffer = settings.lowerThreshold.ToString();
            upperThresholdBuffer = settings.upperThreshold.ToString();
        }

        /// <summary>
        /// 当玩家打开本 Mod 的设置页面时，游戏会调用此方法来绘制窗口内容。
        /// </summary>
        /// <param name="inRect">一个 Rect 对象，代表了可供我们绘制的窗口区域。</param>
        public override void DoSettingsWindowContents(Rect inRect)
        {
            // Listing_Standard 是一个非常方便的工具，可以帮助我们快速创建标准的列表式 UI 布局。
            Listing_Standard listingStandard = new Listing_Standard();
            // 开始在该 Rect 区域内进行绘制
            listingStandard.Begin(inRect);

            // --- 阈值输入框 ---
            // 我们使用 Widgets 类进行更手动的布局，以实现 "标签 | 输入框" 在同一行的效果。
            Rect lowerRect = listingStandard.GetRect(Text.LineHeight); // 从布局中“申请”一行的高度和空间
            Widgets.Label(lowerRect.LeftHalf().Rounded(), "ANP_LowerThresholdLabel".Translate()); // 在左半边绘制标签，并调用翻译
            Widgets.TextFieldNumeric(lowerRect.RightHalf().Rounded(), ref settings.lowerThreshold, ref lowerThresholdBuffer, 0, 9999); // 在右半边绘制数字输入框

            Rect upperRect = listingStandard.GetRect(Text.LineHeight);
            Widgets.Label(upperRect.LeftHalf().Rounded(), "ANP_UpperThresholdLabel".Translate());
            Widgets.TextFieldNumeric(upperRect.RightHalf().Rounded(), ref settings.upperThreshold, ref upperThresholdBuffer, 0, 9999);

            // --- 储料斗控制复选框 ---
            listingStandard.Gap(12f); // 添加 12 像素的垂直间距，让布局更美观
            listingStandard.CheckboxLabeled("ANP_HopperControlLabel".Translate(), ref settings.disableHoppers, "ANP_HopperControlTooltip".Translate());

            // --- 手动应用按钮 ---
            listingStandard.Gap(24f); // 添加一个更大的间距
            // listingStandard.ButtonText() 会绘制一个按钮，如果被点击，则返回 true。
            if (listingStandard.ButtonText("ANP_ApplyNowButton".Translate()))
            {
                TriggerImmediateCheck(); // 如果按钮被点击，则触发立即检查
            }

            // 结束绘制
            listingStandard.End();
            base.DoSettingsWindowContents(inRect);
        }

        /// <summary>
        /// 当用户点击“立即应用”按钮时，调用此方法。
        /// </summary>
        private void TriggerImmediateCheck()
        {
            // 在强制检查前，先执行一次数值校正，确保下限不会超过上限。
            if (settings.lowerThreshold > settings.upperThreshold)
            {
                // 将下限设置为上限的一半 (使用整数除法)
                settings.lowerThreshold = settings.upperThreshold / 2;
                // 别忘了同步更新用于显示的字符串缓冲区
                lowerThresholdBuffer = settings.lowerThreshold.ToString();
            }

            // 确保只在游戏世界已经加载后才执行（防止在主菜单修改时出错）。
            if (Current.Game != null && AutoNutrientPasteManager.Current != null)
            {
                // 调用管理器的立即检查方法。
                AutoNutrientPasteManager.Current.ForceCheckNow();
            }
        }

        /// <summary>
        /// 重写此方法，当玩家关闭设置窗口时，游戏会调用它来保存设置。
        /// 这是执行最终数据验证和校正的绝佳时机。
        /// </summary>
        public override void WriteSettings()
        {
            // 在设置被最终写入文件前，执行一次校正。
            if (settings.lowerThreshold > settings.upperThreshold)
            {
                // 将下限设置为上限的一半
                settings.lowerThreshold = settings.upperThreshold / 2;
                lowerThresholdBuffer = settings.lowerThreshold.ToString();
            }
            base.WriteSettings(); // 调用基类方法来完成实际的写入操作。
        }

        /// <summary>
        /// 返回您的 Mod 在设置菜单中的分类名称。
        /// </summary>
        public override string SettingsCategory() => "ANP_SettingsCategory".Translate();
    }
}
