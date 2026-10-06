# 更新日志 / Changelog

各版本的变化与验收范围。安装方式见 [README](README.md)。

Changes and validation scope by version. See [README](README.md) for installation.

## 0.6.27 — 2026-10-06

### 中文

- 撤销拾荒垃圾场扩容和随身槽重排，恢复原版 12×9 地面网格与右侧口袋／背包槽布局。
- 本次为源码撤销，未打包、安装或原生测试。

### English

- Reverted the scavenging yard expansion and carry-slot rearrangement, restoring the native 12×9 ground grid with pocket/backpack slots on the right.
- Source revert only; not packaged, installed or natively tested.


## 0.6.26 — 2026-10-06

### 中文

本节汇总相对 main（Forge 0.6.18）的当前源码增量。审查候选沿用 0.6.26，版本号不单独证明二进制或原生验收范围。

- 内置共享全域制造终端：统一物品、图标、售价、槽位、电耗和夜间供货；内容 Mod 追加配方。读取时兼容两个已知旧终端 ID，保留身份与内部物品关系。
- 配方手册改为动态读取机器及追加配方目录，提供分类、数量、双语条件和刷新版本；增加只读物品简短说明接口，供手册悬停使用。
- 左侧拾荒垃圾场扩至 16×16，口袋与背包槽移到下方；保留更大已有网格及物品。
- 一次性／高级加速器接入单批加工，成功后才允许原版工具／充能消耗；公共电池扣电同步刷新图标。
- 白天供货取消保底追加，改为与原版候选共同抽取；新增当前天数门槛和矿工独立权重，配套原矿为 70%／15%／15%。
- 整批奖励改用托管占格图预检并共享布局接口；单件搬运核对原生插槽身份、拒收恢复来源，倒液读回实际组分、价值与品质变化。
- 动作钩子延后至本地化就绪，避免启动阶段访问未完成资源；加载／夜间回调按实际原方法执行分派，增加夜晚 attempt 清理与晚期加载通知，拒绝 Load 不清除活状态。
- README 改为完整中英文区块；物流显示名同步为“物流网络”。源码、配套构建、安装与原生验收分别说明。
- 2026-10-06 candidate003 已有选定机器／物流／跨进程读回证据；内置奖励、供货概率、鼠键 UI、完整两晚、旧终端迁移与后续悬停接口仍未完成原生验收。准确范围见源码 docs/FORGE_PROGRESS.md。


### English

This section summarizes current source changes relative to main (Forge 0.6.18). Review candidates retain version 0.6.26; a version string alone does not identify binary or native-validation scope.

- Added the shared Universal Manufacturing Terminal with Forge-owned item, icon, pricing, slots, power and night stock; content mods add recipes. Known legacy IDs are normalized on reads while retaining identity and internal contents.
- Handbooks now read machines and additional recipes dynamically, with categories, counts, bilingual conditions and a refresh revision. Added a read-only short-description API for item hover text.
- Expanded the left scavenging yard to 16×16 and placed pocket/backpack slots below it, preserving larger grids and contents.
- Accelerators run one batch and allow native tool/charge consumption only after success. Public battery debits refresh icons.
- Replaced guaranteed extra daytime stock with shared native/mod draws; added current-day thresholds and independent miner weights, producing matching ore batches at 70%/15%/15%.
- Shared managed whole-reward placement preflight; single-item moves verify native slot identity and restore rejected sources, while pours reread actual composition, value and quality changes.
- Deferred action hooks until localization is ready. Business load/night events require native-body execution; added night-attempt cleanup and late-load notification, preserving live state on declined loads.
- README uses complete Chinese/English blocks; the logistics Chinese display name is 物流网络. Source, builds, installation and native coverage are distinguished.
- Dated candidate003 covers selected machine/logistics/cross-process readback cases. Built-in rewards, supply probabilities, mouse/keyboard UI, two complete nights, legacy migration and later hover APIs remain unvalidated. See source docs/FORGE_PROGRESS.md for exact scope.

## 0.6.17 — 2026-10-04

### 中文

- 原版档案箱内部容量扩为 16×16（256 格），在创建和读档恢复物品前扩容，沿用原物品、位置与收纳规则。
- 更新配套构建至机核协议合集 0.0.3：合成扩展 0.9.41、机械飞升 0.1.39、模组矩阵 1.1.2。

### English

- Expanded the native Dossier inventory to 16×16 (256 cells) on creation and before restoring saved items, preserving contents, positions and item restrictions.
- Updated the matching Mechcore Protocol build to bundle 0.0.3: Synthesis Expansion 0.9.41, Mechanical Ascension 0.1.39 and Module Matrix 1.1.2.

## 0.6.16 — 2026-10-04

### 中文

- 修复 MelonLoader 生成的互操作程序集哈希变化时，受支持的游戏版本被误判，导致内容注册、生命周期与工坊成就被停用的问题。游戏本体版本与实际接口校验继续生效。
- 新游戏选择栏统一使用紧凑行高；选项较多时支持鼠标滚轮和细滚动条，原版与 Mod 开局选项共用。
- 图标固定在栏位左侧并留出文字间距，修复紧凑布局下图标与开局名称重叠的问题。

### English

- Fixed supported game builds being rejected when the hash of MelonLoader's generated interop assembly changes, disabling content registration, lifecycle callbacks and workshop achievements. Native game-build and interface checks remain enforced.
- Added compact rows, mouse-wheel scrolling and a thin scrollbar to the shared native and mod new-game selection list.
- Fixed icons overlapping start names in the compact layout by anchoring icons to the left and reserving text spacing.

## 0.6.15 — 2026-10-04

### 中文

- 机器生产更稳定：同一台机器不会同时处理互相干扰的生产流程，夜间生产与过夜结算更可靠。
- 机器数据不完整时只跳过个别条目，不再影响其他机器的生产。
- 内容 Mod 注册发生冲突时会被拒绝，已经生效的定义不会被覆盖。
- 新增一种价值计算方式，可把内容方提供的批次加值与材料倍率一起分摊并向上取整；原有方式保持不变。
- 本版为本地审查候选，尚未发布或安装到游戏正式目录。

### English

- Machine production is more stable: a machine no longer runs overlapping production work that could interfere with itself, so nightly production and overnight settlement stay reliable.
- Incomplete machine data now skips only the affected entry instead of interrupting the other machines.
- Conflicts during content mod registration are rejected, so definitions that were already accepted are no longer overwritten.
- Added a value calculation option that distributes a content mod's per-batch bonus together with material multipliers and rounds up; the existing calculation is unchanged.
- This is a local review candidate; it has not been released or installed into the live game folder.

## 0.1.0 — 2026-09-27

### 中文

**新增**

- 内容 Mod 共用的前置，可添加物品、设施、节点、机器模组与效果。
- Nico 工坊：多个 Mod 各自提供标签页，Forge 只负责统一展示与操作。
- 兼容的内容 Mod 可以共用开局进度与周目数据。
- 与游戏或其他 Mod 内容重名时，保留已有内容，只停用受到影响的那项能力。

**说明**

- 面向 Probably Stolen Demo Steam Build `25382790`，需要 [MelonLoader](https://melonwiki.xyz/)。其他游戏构建未验证。
- Forge 单独安装不增加玩法内容。

### English

**Added**

- A shared dependency for content mods to add items, facilities, nodes, machine modules and effects.
- Nico Workshop: several mods each bring their own tabs, and Forge handles display and interaction.
- Compatible content mods can share start progress and per-run data.
- When a name clashes with the game or another mod, existing content is kept and only the affected feature is disabled.

**Notes**

- Targets Probably Stolen Demo Steam Build `25382790` and needs [MelonLoader](https://melonwiki.xyz/). Other game builds are unverified.
- Forge adds no gameplay content by itself.
