namespace Nicokobo.Forge;

// Forge 编译内置数值：修改后重新编译框架；具体内容价格、配方和奖励由各 Mod 自行配置。
internal static class ForgeNumbers
{
    internal static class Assets
    {
        internal const int RetryMilliseconds = 250;
    }

    internal static class SaveReadback
    {
        internal const int MaxBytes = 16 * 1024 * 1024;
        internal const int MaxDepth = 256;
    }

    // Forge 内置原版成就；只改本页条件和奖励，不改变原生物品能力。
    internal static class Achievements
    {
        // 工坊名片在原生日用品掉落表内的相对权重。
        internal const float CardLootWeight = 0.05f;
        internal const float RatGrams = 800f;
        internal const int WineDays = 10;
        internal const int WineTopTier = 5;
        internal const int ScannerChance = 100;
        internal const int Wealth = 100000;
        internal const int Attractiveness = 1000;
        internal const int Reputation = 200;
        internal const int SerumCount = 5;
        internal const int VictoryBudgetMultiplier = 2;
        internal const int UpdateMilliseconds = 1000;
        internal const int DragQuietMilliseconds = 250;
        internal const int CardRetrySeconds = 5;
        internal const int SaveRetrySeconds = 5;
        internal const int MaxSaveBytes = 64 * 1024 * 1024;
        internal const int MaxRewardItems = 10;
    }

    // 通用机器模板的默认值与注册校验上限；内容方可按 API 契约声明自己的参数。
    internal static class Machines
    {
        // 默认电池舱宽度（格）。
        internal const int DefaultBatteryWidth = 1;
        // 默认电池舱高度（格）。
        internal const int DefaultBatteryHeight = 1;
        // 默认模组舱宽度（格）。
        internal const int DefaultModuleWidth = 7;
        // 默认模组舱高度（格）。
        internal const int DefaultModuleHeight = 5;
        // 物品输出声明省略数量时的默认产物件数。
        internal const int DefaultOutputCount = 1;
        // 物品输出声明及批次计算允许的最大件数。
        internal const int MaxOutputCount = 256;
        // 机器仓库宽度和高度各自允许的最大格数。
        internal const int MaxGridSide = 32;
        // 单台机器允许声明的液体输入槽数量上限。
        internal const int MaxLiquidInputSlots = 8;
        // 机器生产价值加成允许的最大百分比；不是最终价值倍率。
        internal const int MaxProductionMarkupPercent = 1000;
        // 机器原版 UI 模板中各仓库元素的横向和纵向布局间距（像素）。
        internal const int LayoutSpacing = 4;
        // 机器读档前重置和读档后重绑回调的生命周期优先级；数值越小越先执行。
        internal const int LifecyclePriority = -100;
    }

    // 档案箱尺寸及库存遍历、估价的保护上限；不开放库存搬运能力。
    internal static class Inventory
    {
        // 原版档案箱内部网格的宽度和高度（格）。
        internal const int DossierGridSide = 16;
        // 一次直接库存快照允许读取的物品数量上限。
        internal const int MaxDirectItems = 4096;
        // 遍历整个周目的原生物品时允许处理的物品数量上限。
        internal const int MaxRunItems = 8192;
        // 库存快照中单件物品允许枚举的直接子物品数量上限。
        internal const int MaxChildrenPerItem = 64;
        // 垃圾清理遍历允许访问的图节点数量上限。
        internal const int MaxTrashTraversalNodes = 4096;
        // 垃圾容器处理允许枚举的物品数量上限。
        internal const int MaxTrashContainerItems = 2048;
        // 读取生产材料总价值时允许枚举的子物品数量上限。
        internal const int MaxValuationChildren = 8191;
    }

    internal static class RunData
    {
        // 单个 Mod 周目数据 JSON 允许的最大字符数（不是 UTF-8 字节数）。
        internal const int MaxJsonChars = 1024 * 1024;
        // 周目数据 JSON 允许的最大嵌套深度。
        internal const int MaxJsonDepth = 64;
    }

    // 新游戏开局选择栏；内容 Mod 只添加卡片，共用 Forge 的布局和滚动。
    internal static class StartSelection
    {
        internal const float MaximumRowHeight = 52f;
        internal const float MinimumRowHeight = 44f;
        internal const float IconSize = 32f;
        internal const float IconLeftPadding = 10f;
        internal const float IconTextGap = 10f;
        internal const float TextRightPadding = 8f;
        internal const float RowVerticalPadding = 4f;
        internal const float ScrollbarSpace = 14f;
        internal const float ScrollbarWidth = 6f;
        internal const float ScrollbarInset = 2f;
        internal const float ScrollbarVerticalPadding = 4f;
    }

    // 工坊声明限制、刷新间隔和界面样式；尺寸及字号使用缩放前的设计像素。
    internal static class Workshop
    {
        // 工坊标签／标题校验允许的最大字符数。
        internal const int MaxTitleChars = 48;
        // 工坊允许登记的 Mod 标签页总数上限。
        internal const int MaxTabs = 16;
        // 单个工坊标签页允许声明的节点／条目总数上限。
        internal const int MaxEntries = 20;
        // 单个工坊图谱允许声明的分组数量上限。
        internal const int MaxGroups = 5;
        // 单个工坊分组允许包含的节点数量上限。
        internal const int MaxEntriesPerGroup = 4;
        // 工坊显示快照的刷新间隔（毫秒）。
        internal const int RefreshMilliseconds = 250;
        // 工坊打开／关闭操作的最小间隔（毫秒）。
        internal const int ToggleCooldownMilliseconds = 400;
        // 标签栏每页同时显示的标签页数量。
        internal const int VisibleTabs = 6;
        // 计算工坊界面整体缩放时使用的设计画布宽度（像素）。
        internal const float DesignWidth = 1100f;
        // 计算工坊界面整体缩放时使用的设计画布高度（像素）。
        internal const float DesignHeight = 760f;
        // 工坊主窗口的设计宽度（像素）。
        internal const float WindowWidth = 1060f;
        // 工坊主窗口的设计高度（像素）。
        internal const float WindowHeight = 710f;
        // 工坊界面的最小缩放倍率；1 表示设计尺寸，0.55 表示 55%。
        internal const float MinimumScale = 0.55f;
        // 相邻工坊标签按钮之间的设计间距（像素）。
        internal const float TabGap = 8f;
        // 工坊图谱节点按钮的设计宽度（像素）。
        internal const float NodeWidth = 116f;
        // 工坊图谱节点按钮的设计高度（像素）。
        internal const float NodeHeight = 62f;
        // 工坊主标题字号（设计像素）。
        internal const int TitleFontSize = 27;
        // 工坊统计栏文字字号（设计像素）。
        internal const int StatFontSize = 14;
        // 节点详情标题字号（设计像素）。
        internal const int DetailTitleFontSize = 22;
        // 节点说明正文的字号（设计像素）。
        internal const int BodyFontSize = 15;
        // 材料／费用文字的字号（设计像素）。
        internal const int CostFontSize = 13;
        // 次要提示文字的字号（设计像素）。
        internal const int MutedFontSize = 13;
        // 工坊分区标题字号（设计像素）。
        internal const int SectionFontSize = 14;
        // 普通工坊按钮文字字号（设计像素）。
        internal const int ButtonFontSize = 13;
        // 购买／解锁按钮文字字号（设计像素）。
        internal const int PurchaseFontSize = 17;
        // 工坊关闭按钮文字字号（设计像素）。
        internal const int CloseFontSize = 18;
        // 节点状态文字的字号（设计像素）。
        internal const int StateFontSize = 15;
    }

    internal static class Diagnostics
    {
        // 库存拖拽探针最多记录的不同槽位数量。
        internal const int MaxDragSlots = 128;
        // 库存拖拽故障探针最多输出的错误日志条数。
        internal const int MaxDragFaultLogs = 8;
    }

    // 原生单位契约，修改时必须同步适配原生表示。
    // 原生液体单位契约；修改时必须同步适配原生表示。
    internal static class NativeUnits
    {
        // 原生液体每毫升对应的组分单位数。
        internal const int LiquidPartsPerMillilitre = 1000;
        // 原生液体每信用点对应的组分单位数，用于基础价值换算。
        internal const decimal LiquidPartsPerCredit = 25000m;
    }
}
