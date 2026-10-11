# Nicokobo Forge

[English](#english) · [中文](#中文)

## English

Author: Nicokobo · Version: **0.6.35** · Target: **Probably Stolen Demo, Steam Build `25382790`**

Nicokobo Forge provides shared registration, native adapters, machine templates, resource transactions and Nico Workshop for content mods. It includes the Universal Manufacturing Terminal, a workshop card for every start and ten base-game achievements. Content mods supply their own items, recipes, prices, growth and unlock pages.

### Installation and updates

1. Install [MelonLoader](https://melonwiki.xyz/) and close the game.
2. Copy `Nicokobo.Forge-0.6.35.dll` and required content mod DLLs into `Mods/`.
3. Remove older DLLs of the same assemblies. Keep one matching Forge DLL when multiple packages include it; versioned filenames can stay unchanged.

Forge can run alone for its workshop and native achievements.

#### Optional compatibility patches

`Nicokobo.CompatibilityPatches-0.1.3.dll` is optional and separate from Forge. It defers Wage's Perks **1.3.4** startup mounts until localization is initialized, preventing early text-read crashes. Copy it to `Mods/` only for that version; remove it to disable. Forge does not install or load it automatically. Missing or mismatched targets disable the patch and log the reason without affecting other mods.


### In-game tools

#### Universal Manufacturing Terminal

Forge owns K05: 3×3 footprint, separate 9×6 input/output grids, a 7×5 ring-shaped module bay, base value 400 and base power 25. It processes one batch per night. Synthesis, Aug and Logistics Nexus add recipes; Forge alone has none.

Night-shop stock has a 40% listing chance, including while owned. Purchases do not restock immediately; full shelves skip it, and daytime NPC supply excludes it.

Two known legacy Synthesis terminal IDs migrate on loading, preserving identity, state, shape and internal item relationships. The next normal save uses the current ID.

#### G recipe browser

Hover an item and press **G** to view recipes that produce or consume it; machines also show their processing recipes. Press G over another item to switch queries, or over the same kind, window or empty space to close. With no target, browse the registered catalog at the last scene-local page. Typing or an open workshop suppresses the shortcut.

Machine and additional recipes appear automatically. Content mods may declare native-machine extension recipes while retaining processing logic and physical guides. Forge alone can open its registered catalog.

#### Nico Workshop

Every start receives a workshop card. Press `N` or double-click the card to open ten native achievements, tracking progress, completion and one reward per run. A full backpack delays card delivery or retains reward eligibility; the final badge uses no inventory slot. Aug adds its own page for the Aug start.

Claiming and confirming Established Proprietor doubles NPC purchase budgets for current and future customers in that run; sales still deduct remaining funds. This stacks with the Aug ×2 budget for ×4 total. Confirmed old reward records apply after loading; new runs must qualify again.

### Save recovery after uninstalling a mod

From Forge 0.6.32, cleanup covers ordinary items registered through Forge with a saved provider record. It requires missing native registration, no current Forge declaration and a confirmed absent provider DLL. Unregistered third-party items, saves without records, installed providers that fail to load/register and uncertain provider status retain their items.

Before uninstalling a content mod, save normally with that mod and the newer Forge installed to record its provider, then keep Forge installed. Legacy manufacturing terminals still use their ID migration.

Eligible player-owned missing items refund saved unit value × count. Normal contents of missing machines/containers retain identity, state, count, charge, quality and nested contents. Available factories and space return them to the shop, backroom or storage; otherwise recovery data keeps them for later loading or before a normal save. Normal items are not converted to money. Sold, unowned and unreachable items grant no refund.

Inventory, balance and recovery data persist together on the next native normal save. Forge does not directly rewrite save files, and repeated loading does not accumulate refunds.

### Shared features

- Native Dossier capacity expands to 16×16 (256 cells), preserving contents and admission rules.
- Compact new-game rows support wheel scrolling and a scrollbar.
- Disposable/advanced accelerators process one immediate Forge batch, consuming the tool or charge only on success; missing inputs, power or space keep the tool.
- Handbooks read current machines/additional recipes, counts, categories and bilingual conditions, refreshing after changes.
- Dynamic output selection runs once per batch. Matrix owns K02's random Rare pool, tiers and prices; reading recipes does not draw an output.
- Single-item transfers and pours verify both ends; batch rewards preflight placement. Content mods implement full resource/power networks.

#### Supply rules

Daytime supply and opted-in Jackson night-module slots filter by NPC, category, day, ownership and enabled content, then share total Mod weight `0.25` equally across eligible kinds. Direct native results have weight `1`, giving about `20%` combined Mod chance; native tables keep their other weights. Mods may retain original native-table weights instead of sharing that budget. Separate night-shop conditions remain, and Mechcore miners draw one batch type at native ore 70%, Quartz 15%, Titanium Ore 15%.

### Configuration and validation

Default logging is `INFO`; existing `UserData/MelonPreferences.cfg` settings take precedence. Set `DEBUG` explicitly and restart for diagnostics. Framework defaults are compiled into the DLL; content mods document their runtime configuration.

The 2026-10-06 candidate records machine processing, selected local logistics and save readback across processes. Mouse/keyboard UI, stock weights, built-in workshop rewards, two full nightly settlements, real legacy-terminal migration and missing-mod recovery/save-reload remain unvalidated. Recovery has offline checks and Release-build evidence. Later source does not inherit earlier binary results; check source, package and loaded versions separately. See source `docs/FORGE_PROGRESS.md` for scope and [change.log](change.log) for changes.

Released under the MIT License.

---

## 中文

作者：Nicokobo · 配套版本：**0.6.35** · 适用：**Probably Stolen Demo，Steam Build `25382790`**

Nicokobo Forge 为内容 Mod 提供通用注册、原生适配、机器模板、资源事务和 Nico 工坊，内置全域制造终端、全开局工坊名片与十项原版成就。各内容 Mod 维护自己的物品、配方、价格、成长和解锁页。

### 安装与更新

1. 安装 [MelonLoader](https://melonwiki.xyz/)，关闭游戏。
2. 将 `Nicokobo.Forge-0.6.35.dll` 和所需内容 Mod DLL 放入 `Mods/`。
3. 移出同程序集旧 DLL。多个包附带 Forge 时只保留一份配套文件，版本号文件名可保留。

Forge 可单独使用内置工坊与原版成就。

#### 可选兼容补丁

`Nicokobo.CompatibilityPatches-0.1.3.dll` 为独立可选组件，将 Wage's Perks **1.3.4** 的启动挂载延后至本地化初始化完成，避免过早读取文本导致崩溃。使用该版本时才复制到 `Mods/`，移出即可停用；Forge 不会自动安装或加载。目标缺失或版本不符时补丁自行停用并记录原因，不影响其他 Mod。


### 游戏内功能

#### 全域制造终端

K05 全域制造终端由 Forge 提供：外部 3×3，输入／输出仓各 9×6，模组仓为 7×5 环形；基础价值 400、基础耗电 25，每晚一批。合成扩展、机械飞升和物流网络追加配方；仅装 Forge 时没有制造配方。

夜店每次生成库存有 40% 上架机会，持有后仍可出现；购买不即时补货，货架满时跳过，不参加白天 NPC 供货。

读档迁移两个已知旧合成扩展终端 ID，保留身份、状态、占格和内部物品关系，下次正常保存使用当前 ID。

#### G 键合成表

指向物品按 **G** 查看其作为产物和材料的配方，指向机器另列自身加工配方。指向另一件物品按 G 切换；同种物品、窗口或空白处按 G 关闭。无目标时浏览完整登记目录，保留本场景阅读页；输入文字或打开工坊时不触发。

机器和追加配方自动进入目录；原版机器扩展由内容 Mod 提交展示，实际加工和实体手册仍由其负责。仅装 Forge 也能打开已登记目录。

#### Nico 工坊

所有开局获赠工坊名片，按 `N` 或双击打开。原版页有十项成就，分别记录进度、达成和领奖，每项每周目领取一次。背包满时延迟名片发放或保留领奖资格；通关徽章不占库存。机械飞升另有专属页，仅对应开局开放。

领取并确认“立业有成”后，本周目当前和后续 NPC 收购预算×2，成交仍正常扣减资金；与伪人预算×2叠加为×4。有效旧领奖记录载入后生效，新周目需重新达成。

### 卸载 Mod 后的存档恢复

Forge 0.6.32 起仅清理通过 Forge 注册且存档已有提供者记录的普通物品；须同时满足原生登记缺失、当前无 Forge 声明、确认提供者 DLL 已移除。未登记的第三方物品、无记录旧档、仍安装但加载／注册失败或来源无法确认的项均保留。

卸载内容 Mod 前，先在该 Mod 与新版 Forge 仍安装时正常保存，建立提供者记录；卸载后继续保留 Forge。旧制造终端仍按原有 ID 迁移。

玩家持有的合格缺失项按存档单价×数量返还信用点。缺失机器／容器内的正常物品保留身份、数量、状态、充能、品质和嵌套内容；工厂与空间可用时放回店内、后房或储物容器，否则留在周目待返还数据中，下次读档或正常保存前重试，不折算成钱。已售、未持有或不可达物品不返款。

库存、余额和待返还数据随下一次原版正常保存一并写入，不直接改写存档；重复读档不累计返款。

### 公共功能

- 原版档案箱扩为 16×16（256 格），保留内容与收纳规则。
- 新游戏选择栏使用紧凑行高，支持滚轮与滚动条。
- 一次性／高级加速器可立即加工一批，成功才消耗工具或充能；缺料、缺电或空间不足时保留工具。
- 手册读取当前机器和追加配方、数量、分类与双语条件，随内容更新刷新。
- 每批动态选择一次产物；模组矩阵维护 K02 随机稀有池、分类和价格，阅读配方不触发抽取。
- 单件搬运和倒液核对两端变化，整批奖励预检占格；完整资源／电力网络由内容 Mod 实现。

#### 供货规则

白天供货与显式加入的杰克逊夜间模组名额按 NPC、类别、天数、持有和启用状态筛选，合格 Mod 种类均分总权重 `0.25`。直接供货原版结果权重为 `1`，Mod 合计概率约 `20%`；原生表保留其他条目权重，内容 Mod 可选择保留原权重、不分摊该预算。独立夜店条件继续生效，机核矿工每批按原版矿石 70%、石英 15%、钛矿料 15% 选矿。

### 配置与验证

默认日志为 `INFO`，已有 `UserData/MelonPreferences.cfg` 设置优先；详细诊断需显式设为 `DEBUG` 并重启。框架默认数值随 DLL 编译，内容 Mod 运行时配置见各自说明。

2026-10-06 候选记录了机器加工、部分本地物流与跨进程保存读回。鼠键 UI、供货权重、内置工坊奖励、完整两晚结算、真实旧终端迁移和卸载 Mod 后的返还／保存重载仍待验收；返还流程已有离线检查和 Release 编译证据。后续源码不继承旧二进制结论，源码、包内与实际加载版本分别核对。范围见源码 `docs/FORGE_PROGRESS.md`，版本变化见 [change.log](change.log)。

本项目以 MIT 许可证发布。
