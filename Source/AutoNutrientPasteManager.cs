// ======================================================================
// 文件名: AutoNutrientPasteManager.cs
// 模块: 核心逻辑管理器
// 职责: 1. 跟踪所有被管理的营养膏机及其生产模式。
//       2. 定期检查库存，并根据用户设定的模式执行生产逻辑。
//       3. 管理与“智能模式”相关的储料斗过滤器（禁用/恢复）。
//       4. 处理存档和读档。
// ======================================================================

// 引入所有必要的命名空间
using System.Collections.Generic; // 用于使用 List<> 和 Dictionary<>
using System.Linq;                // 用于使用 LINQ 查询 (e.g., .Where(), .Sum())
using RimWorld;                   // RimWorld 的核心游戏逻辑
using Verse;                      // RimWorld 的基础类和工具

namespace AutoNutrientPaste
{
    /// <summary>
    /// 定义了营养膏机的三种生产模式，作为一个枚举类型，让代码更清晰、更安全。
    /// </summary>
    public enum ProductionMode
    {
        Off,        // Mod 功能关闭
        Smart,      // 智能模式：根据上下限阈值生产
        Continuous  // 连续模式：无视阈值，持续生产
    }

    /// <summary>
    /// 全局管理器，继承自 GameComponent，与存档游戏绑定。
    /// </summary>
    public class AutoNutrientPasteManager : GameComponent
    {
        // ======================================================================
        // 常量与字段
        // ======================================================================
        private const int CheckIntervalTicks = 120; // 检查间隔 (120 ticks = 2秒)，定义为常量以方便修改和提高可读性

        // 核心数据结构：一个字典，用于存储每个被管理的机器及其当前的生产模式。
        // 这取代了之前分散的 List 和 bool 变量，让状态管理更集中。
        private Dictionary<Building_NutrientPasteDispenser, ProductionMode> managedDispensers;

        // 存储被“智能模式”禁用的储料斗的原始过滤器设置，以便将来恢复。
        private Dictionary<Building_Storage, ThingFilter> savedHopperSettings;

        // Scribe 解析对象引用时会跨 LoadingVars/ResolvingCrossRefs 多个阶段工作。
        // 因此字典的临时 key/value 列表必须是实例字段，不能是 ExposeData 内的局部变量；
        // 否则第一阶段读到的数据会在第二阶段前丢失，导致机器模式读档后变回 Off。
        private List<Building_NutrientPasteDispenser> managedDispenserKeys;
        private List<ProductionMode> managedDispenserModes;
        private List<Building_Storage> savedHopperKeys;
        private List<ThingFilter> savedHopperFilters;

        // “智能模式”的内部状态变量。
        // true:  数量低于下限，应处于生产周期。
        // false: 数量达到上限，应处于停止周期。
        private bool isSmartModeProducing;

        // 一个静态的单例引用，方便从UI等其他地方访问到当前的管理器实例。
        public static AutoNutrientPasteManager Current { get; private set; }

        // ======================================================================
        // 构造与初始化
        // ======================================================================
        /// <summary>
        /// GameComponent 的构造函数。在对象创建时被调用，是初始化所有集合的黄金标准。
        /// </summary>
        public AutoNutrientPasteManager(Game game)
        {
            Current = this;
            // 在构造函数中初始化所有集合，确保它们在任何时候都不是 null，从根本上杜绝 NullReferenceException。
            managedDispensers = new Dictionary<Building_NutrientPasteDispenser, ProductionMode>();
            savedHopperSettings = new Dictionary<Building_Storage, ThingFilter>();
            isSmartModeProducing = false;
        }

        // ======================================================================
        // 公共方法 (供UI调用)
        // ======================================================================

        /// <summary>
        /// 获取指定机器当前的生产模式。
        /// </summary>
        public ProductionMode GetProductionMode(Building_NutrientPasteDispenser dispenser)
        {
            // TryGetValue 是访问字典最安全的方式，它不会因为 Key 不存在而抛出异常。
            if (managedDispensers.TryGetValue(dispenser, out ProductionMode mode))
            {
                return mode;
            }
            return ProductionMode.Off; // 如果机器不在我们的管理列表中，则默认为关闭状态。
        }

        /// <summary>
        /// 设置指定机器的生产模式。这是两个UI按钮的核心交互方法。
        /// </summary>
        public void SetProductionMode(Building_NutrientPasteDispenser dispenser, ProductionMode newMode)
        {
            ProductionMode currentMode = GetProductionMode(dispenser);

            // 如果玩家点击了当前已经激活的模式按钮，则将其关闭。
            if (currentMode == newMode)
            {
                managedDispensers.Remove(dispenser);
                RestoreHopperSettingsFor(dispenser, true); // 确保在关闭时，强制恢复与之相连的储料斗。
            }
            else
            {
                // 否则，就切换到新模式。
                // 在切换到任何新模式（智能或连续）之前，都先恢复一次储料斗，确保它们处于可用状态。
                RestoreHopperSettingsFor(dispenser, true);
                managedDispensers[dispenser] = newMode; // 使用索引器可以安全地添加新条目或更新现有条目。
            }
        }

        /// <summary>
        /// 当用户在设置窗口点击“立即应用”时，强制触发一次逻辑检查。
        /// </summary>
        public void ForceCheckNow()
        {
            // 通过调用 GameComponentTick(true) 来立即执行一次完整的逻辑检查，而不用等待计时器。
            GameComponentTick(true);
        }

        // ======================================================================
        // 核心 Tick 逻辑
        // ======================================================================

        /// <summary>
        /// GameComponent 的核心 Tick 方法，由游戏引擎定期调用。
        /// 我们将其重载，以接受一个 `force` 参数。
        /// </summary>
        public void GameComponentTick(bool force = false)
        {
            // 如果不是强制执行，并且还没到检查时间，就直接返回。
            if (!force && Find.TickManager.TicksGame % CheckIntervalTicks != 0) return;
            // 如果没有任何机器被管理，也直接返回。
            if (managedDispensers.Count == 0) return;

            // 定期清理已经不存在的对象，防止内存泄漏。
            CleanupDestroyedItems();

            int currentMeals = GetTotalMealCount(Find.CurrentMap);
            int lowerThreshold = AutoNutrientPasteMod.settings.lowerThreshold;
            int upperThreshold = AutoNutrientPasteMod.settings.upperThreshold;

            // 1. 更新“智能模式”的全局状态
            bool oldSmartModeState = isSmartModeProducing;
            if (currentMeals < lowerThreshold) { isSmartModeProducing = true; }
            else if (currentMeals >= upperThreshold) { isSmartModeProducing = false; }

            // 2. 仅当“智能模式”的状态发生切换时，才执行一次性的储料斗禁用/恢复操作
            if (AutoNutrientPasteMod.settings.disableHoppers && oldSmartModeState != isSmartModeProducing)
            {
                if (isSmartModeProducing) { RestoreAllHoppersIfNeeded(); } // 从 停止 -> 生产
                else { DisableAllHoppersAndSaveSettings(); }               // 从 生产 -> 停止
            }

            // 3. 遍历所有被管理的机器，根据各自的模式和当前状态执行生产
            foreach (var pair in managedDispensers)
            {
                var dispenser = pair.Key;
                var mode = pair.Value;

                if (dispenser == null || dispenser.Destroyed) continue;

                // 只有当模式是无限，或者模式是智能且处于生产状态时，才恢复该机器的储料斗
                if (mode == ProductionMode.Continuous || (mode == ProductionMode.Smart && isSmartModeProducing))
                {
                    if (AutoNutrientPasteMod.settings.disableHoppers) { RestoreHopperSettingsFor(dispenser, false); }
                }

                // 根据机器的模式决定生产行为
                switch (mode)
                {
                    case ProductionMode.Smart:
                        if (isSmartModeProducing) { ProduceMeals(dispenser, upperThreshold); }
                        break;
                    case ProductionMode.Continuous:
                        ProduceMeals(dispenser, int.MaxValue); // 传入 int.MaxValue 作为“无限”的上限
                        break;
                }
            }
        }

        /// <summary>
        /// 这是游戏实际调用的 override 方法，它只是简单地调用我们的带参数版本。
        /// </summary>
        public override void GameComponentTick()
        {
            GameComponentTick(false);
        }

        // ======================================================================
        // 私有辅助方法
        // ======================================================================

        private void ProduceMeals(Building_NutrientPasteDispenser dispenser, int stopAtCount)
        {
            // 在循环开始前获取一次总数，进行微优化
            int mealCount = GetTotalMealCount(dispenser.Map);
            // 使用 while 循环，在同一个 Tick 内快速连续生产
            while (dispenser.CanDispenseNow && mealCount < stopAtCount)
            {
                Thing meal = dispenser.TryDispenseFood();
                if (meal == null) break;
                GenPlace.TryPlaceThing(meal, dispenser.InteractionCell, dispenser.Map, ThingPlaceMode.Near);
                mealCount++; // 使用本地变量递增，比反复调用 GetTotalMealCount() 性能更好
            }
        }

        private int GetTotalMealCount(Map map)
        {
            if (map == null) return 0;
            // 使用 LINQ 的 Sum() 方法，代码更简洁
            return map.listerThings.ThingsOfDef(ThingDefOf.MealNutrientPaste).Sum(t => t.stackCount);
        }

        private void DisableAllHoppersAndSaveSettings()
        {
            // 只对处于“智能模式”的机器执行此操作
            var smartModeDispensers = managedDispensers.Where(p => p.Value == ProductionMode.Smart).Select(p => p.Key);
            foreach (var dispenser in smartModeDispensers)
            {
                if (dispenser == null || dispenser.Destroyed) continue;
                foreach (var cell in GenAdj.OccupiedRect(dispenser).AdjacentCells)
                {
                    var hopper = cell.GetFirstThing<Building_Storage>(dispenser.Map);
                    if (hopper != null && hopper.def == ThingDefOf.Hopper)
                    {
                        var backupFilter = new ThingFilter();
                        backupFilter.CopyAllowancesFrom(hopper.GetStoreSettings().filter);
                        savedHopperSettings[hopper] = backupFilter; // 使用索引器安全地添加或更新
                        hopper.GetStoreSettings().filter.SetDisallowAll();
                    }
                }
            }
        }

        private void RestoreAllHoppersIfNeeded()
        {
            if (savedHopperSettings.Count == 0) return;
            var hoppersToRestore = new List<Building_Storage>(savedHopperSettings.Keys);
            foreach (var hopper in hoppersToRestore)
            {
                // 智能恢复逻辑：如果用户没动过，就恢复；如果动过了，就承认用户的修改
                if (hopper != null && !hopper.Destroyed && hopper.GetStoreSettings().filter.AllowedDefCount == 0)
                {
                    RestoreHopperSettingsFor(hopper, false);
                }
                else
                {
                    // 用户已经手动修改了，我们只需将它从备份列表中移除即可
                    savedHopperSettings.Remove(hopper);
                }
            }
        }

        private void RestoreHopperSettingsFor(Building_Storage hopper, bool forceClear)
        {
            if (hopper == null || !savedHopperSettings.TryGetValue(hopper, out ThingFilter filterToRestore)) return;
            // forceClear 为 true (如手动开关按钮时)，或用户没有手动修改过过滤器时，才执行恢复
            if (forceClear || hopper.GetStoreSettings().filter.AllowedDefCount == 0)
            {
                hopper.GetStoreSettings().filter.CopyAllowancesFrom(filterToRestore);
            }
            savedHopperSettings.Remove(hopper);
        }

        private void RestoreHopperSettingsFor(Building_NutrientPasteDispenser dispenser, bool forceClear)
        {
            if (dispenser == null || dispenser.Destroyed) return;
            foreach (var cell in GenAdj.OccupiedRect(dispenser).AdjacentCells)
            {
                var hopper = cell.GetFirstThing<Building_Storage>(dispenser.Map);
                if (hopper != null)
                {
                    RestoreHopperSettingsFor(hopper, forceClear);
                }
            }
        }

        private void CleanupDestroyedItems()
        {
            var destroyedDispensers = managedDispensers.Keys.Where(d => d == null || d.Destroyed).ToList();
            foreach (var dispenser in destroyedDispensers) { managedDispensers.Remove(dispenser); }

            var destroyedHoppers = savedHopperSettings.Keys.Where(h => h == null || h.Destroyed).ToList();
            foreach (var hopper in destroyedHoppers) { savedHopperSettings.Remove(hopper); }
        }

        // ======================================================================
        // 存档与读档
        // ======================================================================
        public override void ExposeData()
        {
            base.ExposeData();

            // 最佳实践：先读写简单值
            Scribe_Values.Look(ref isSmartModeProducing, "isSmartModeProducing", false);

            // 再读写集合。这些临时列表必须保留在实例上，以便 Scribe 跨读档阶段解析 Thing 引用。
            Scribe_Collections.Look(ref managedDispensers, "managedDispensers", LookMode.Reference, LookMode.Value,
                ref managedDispenserKeys, ref managedDispenserModes);
            Scribe_Collections.Look(ref savedHopperSettings, "savedHopperSettings", LookMode.Reference, LookMode.Deep,
                ref savedHopperKeys, ref savedHopperFilters);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // 旧存档或部分损坏的存档可能没有这些节点，确保后续 UI/Tick 始终可安全访问。
                if (managedDispensers == null)
                    managedDispensers = new Dictionary<Building_NutrientPasteDispenser, ProductionMode>();
                if (savedHopperSettings == null)
                    savedHopperSettings = new Dictionary<Building_Storage, ThingFilter>();

                // 忽略已被删除、无法解析或无效的条目，避免它们影响正常模式。
                var invalidDispensers = managedDispensers
                    .Where(pair => pair.Key == null || pair.Key.Destroyed || pair.Value == ProductionMode.Off)
                    .Select(pair => pair.Key)
                    .ToList();
                foreach (var dispenser in invalidDispensers)
                    managedDispensers.Remove(dispenser);

                var invalidHoppers = savedHopperSettings
                    .Where(pair => pair.Key == null || pair.Key.Destroyed || pair.Value == null)
                    .Select(pair => pair.Key)
                    .ToList();
                foreach (var hopper in invalidHoppers)
                    savedHopperSettings.Remove(hopper);

                managedDispenserKeys = null;
                managedDispenserModes = null;
                savedHopperKeys = null;
                savedHopperFilters = null;
            }
        }
    }
}
