# Changelog / 更新日志

What each released version of Nicokobo Forge changed. Install steps are in the [README](README.md).

各发布版本改了什么。安装步骤见 [README](README.md)。

## 0.6.6 库存读取与拖放刷新 — 2026-10-03

- Provide detached owned-item counts shared within one frame, invalidated before and after native transfers, quantity/ownership changes and run boundaries. Fresh transaction captures retain their original behavior.
- Defer background achievement scans, automatic saves and card delivery during native dragging and for 250 ms afterwards. Save/victory boundaries still check immediately; tool-removal proof captures only tool ownership without store valuation.
- Capture relevant single-item proof before quantity changes. Domain/build/install evidence remains distinct from native drag performance.

## 0.6.4 机器生产倍率透传 — 2026-10-02

- Machine definitions may carry a content-owned production markup from 0 to 1000 percent.
- The configured value is copied into every batch context, so contributors can apply the target machine's rate to additional recipes.
- Forge validates and passes through the value; content mods continue to own their pricing rules and factory defaults.
- Packaging, static checks and in-game processing are tracked separately for this local candidate.

## 0.6.4 机器生产倍率透传 — 2026-10-02

- 机器定义新增内容方持有的生产倍率，范围为 0–1000%。
- 每批上下文包含机器配置值，追加配方可以沿用目标机器的倍率。
- Forge 只校验和传递倍率；配方定价规则与默认工厂价值仍由内容 Mod 管理。
- 本地候选的构建、静态检查和原生生产验收分开记录。

## 0.6.3 生产标签读回修复 — 2026-10-01

- 写入生产基础价值、液体价值账本和机器隔离标记后同步原生有效状态，确保只读接口读到已写入的值。
- 修复合成扩展创建组件时报 `Production basis readback mismatch`；新增有效状态快照分离的 API 回归检查。
- 配套合成扩展 0.9.8 修复品级标签、生物质罐罐体价格与手册内容同步；稳定 ID、配方、默认价与图标沿用当前定义。

## 0.6.2 启动初始化修复 — 2026-10-01

- 液体价值补丁仅接入容器变化与物品价值读取，避免启动时触发 `WaterFeatureHelper` 的原生静态初始化和同步本地化加载。
- 修复液体补丁安装失败后机器加工停用，以及随后主菜单出现无效资源操作句柄的问题；补丁失败日志包含具体目标和内部异常。
- 49 项签名、领域检查与示例编译通过。新进程主菜单启动未再出现上述异常，20 条 schema 6 配方正常读取，熔炉扩展恢复加载；实际过夜与存档重载未在本轮验收。

## 0.6.1 生产价值接口（本地候选）— 2026-10-01

- 本批上下文记录选中材料与实际耗液的内在价值；内容方声明生产倍率和原料品质去重，工厂默认价由内容方独立保留。
- 固体产物保存品质前基础价值；液体快照、保存标签、原生倾倒、混合、部分消耗和恢复同时保留实际／品质前价值。
- 价值阶段及附属物列表使用游戏原生接口；制造成本排除市场与事件修正。投入可按原生类别声明，供不同曲目的音乐磁带等配方使用。
- 49 项离线签名、领域检查、Release／Obfuscar 及四个配套包的共享哈希检查通过。本轮未安装或执行原生生产、成交、倾倒与保存重载测试。

## 0.6.0 机器投放与生产修正（本地候选）— 2026-10-01

- 合成扩展 K01–K05 的物品输入为 9×6，自动投放通过原生接纳、形状及堆叠判断选择电池、配方材料、储水输入和输出容器槽。作为原料的生物质罐在 K05 优先进入物品输入。
- 输入白名单读取当前机器的全部配方，包含其他 Mod 追加内容；目录变化后重新绑定白名单。
- 新物品输出仓保留原版禁止玩家插入的规则；生产事务临时绕过该接纳限制，保留原生容量检查，并在成功或失败后恢复限制。
- 液体容量使用原生剩余容量加已有体积；修正把装满百分比当作容量的错误。
- 新进程原生探针通过全部 27 条配方，覆盖耗电、材料消耗、灌装量、失败时资源保留、六类机器公共过夜入口和同夜去重；另一个进程通过六类机器 JSON 读回。完整游戏过夜与实际鼠标操作另行验收。

- 框架统一提供净水器同款储水容器槽与原生背景；液体输入默认使用游戏本地化的“储水容器”，容器输出保留原生“输出”标题。
- 可选液体输入只由配方决定是否消耗；现有容器输出与物品输出分别声明。K04 使用供水容器加输出容器，K05 使用可选容器输入。
- 修正槽位装配重载：标题在上、库存在下，液体输入放在物品输入与输出之间，备注位于最右侧。
- 默认采用原生 7×5 环形模组仓；电池和容器使用随装入物品扩展的原生槽。自定义矩形仓新建库存，不残留原环形背景。
- 新进程的 8 台临时机器通过原生槽位位置与形状读回；正常游戏画面、过夜和存档重载另行验收。

The machine layout now uses explicit native cell coordinates, native label rows, the full module ring, and container slots that grow to fit their items. Eight temporary machines passed native slot readback checks in a fresh process. Normal-session rendering, nightly production and save/reload remain separate checks.

## 0.1.0 — 2026-09-27

### English

**Added**

- A shared dependency for content mods to register game items, facilities, nodes, machine modules, and effects.
- Nico Workshop support, where multiple mods contribute their own tabs and Forge handles display and event dispatch.
- Shared start-ID claiming and per-run data support for compatible content mods.
- Safeguards for ID conflicts with existing game or mod entries: other extensions keep their results, and only the affected Forge capability is disabled.

**Notes**

- Targets Probably Stolen Demo Steam Build `25382790` and needs [MelonLoader](https://melonwiki.xyz/). Other game builds are unverified.
- Forge adds no gameplay content by itself.

### 中文

**新增**

- 内容 Mod 共用前置：注册游戏物品、设施、节点、机器模组与效果。
- Nico 工坊支持：多个 Mod 各自贡献标签页，Forge 只负责展示与事件分派。
- 为兼容的内容 Mod 提供开局 ID 认领与周目数据支持。
- 保护措施：与游戏或其他 Mod 条目 ID 冲突时，保留其他扩展的结果，只停用受影响的 Forge 能力。

**说明**

- 面向 Probably Stolen Demo Steam Build `25382790`，需要 [MelonLoader](https://melonwiki.xyz/)；其他游戏构建未验证。
- Forge 单独安装不增加玩法内容。
