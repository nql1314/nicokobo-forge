# Changelog / 更新日志

What each released version of Nicokobo Forge changed. Install steps are in the [README](README.md).

各发布版本改了什么。安装步骤见 [README](README.md)。

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
