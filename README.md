# Nicokobo Forge

[English](#english) · [中文](#中文)

## English

Author: Nicokobo · Source version: **0.6.34** · Target: **Probably Stolen Demo, Steam Build `25382790`**

Nicokobo Forge provides shared registration, native adapters, machine templates, resource transactions and Nico Workshop for content mods. It includes the Universal Manufacturing Terminal, a workshop card for every start and ten base-game achievements. Content mods supply their own items, recipes, prices, growth and unlock pages.

0.6.34 adds shared dialogue, named client visits and reading windows. See [Conversation API](docs/CONVERSATION_API.md).

### Installation and updates

1. Install [MelonLoader](https://melonwiki.xyz/) and close the game.
2. Copy `Nicokobo.Forge-0.6.34.dll` and required content mod DLLs into `Mods/`.
3. Remove older DLLs of the same assemblies. Keep one matching Forge DLL when multiple packages include it; versioned filenames can stay unchanged.

Forge can run alone for its workshop and native achievements.

#### Optional compatibility patches

`Nicokobo.CompatibilityPatches-*.dll` is optional and separate from Forge. It defers Wage's Perks **1.3.4** startup mounts until localization is initialized, preventing early text-read crashes. Copy it to `Mods/` only for that version; remove it to disable. Forge does not install or load it automatically. Missing or mismatched targets disable the patch and log the reason without affecting other mods. See `compatibility/README.md`.

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

### Development

Framework defaults are compiled from [BuildConfig](BuildConfig/README.md); content-mod values live in the [companion BuildConfig](../probably-stolen/mods-melonloader/BuildConfig/README.md). Check `ForgeCapabilities.Current` before actions: `Accepted` registration and `Applied` native directories are separate states. `InventoryTransfer` remains `false`; single-item move/pour APIs have narrower contracts.

Forge also provides stable IDs/owners/conflict diagnostics, lifecycle/localization hooks, run data, module descriptions, liquid/value ledgers, workshop tabs/unlock graphs, embedded atlases, read-only ES3 access and native patch-target checks. `NpcTrade.Suppliers` restricts daytime suppliers; `IncludeInNightShop` opts modules into Jackson's native night slots.

Run in the Forge repository:

```powershell
.\scripts\Build-P0.ps1 -GameDir 'F:\SteamLibrary\steamapps\common\Probably Stolen Demo'
```

This checks/builds Forge and four examples; `-IncludeProbes` adds workshop/effect probes. Run in the companion `probably-stolen` repository:

```powershell
.\mods-melonloader\Build-ForgeMods.ps1 -NoInstall
```

Without `-NoInstall`, `Build-ForgeMods.ps1` also installs Forge and four content mods and removes older copies; `Build-P0.ps1` only checks/builds. See [development](docs/DEVELOPMENT.md) for environment, output, logs and dependency selection.

#### Documentation

- [Index](docs/README.md): APIs, examples and historical evidence.
- [API responsibilities](docs/API_BOUNDARIES.md) and [scope](docs/SCOPE.md).
- [Machine API](docs/MACHINE_API.md), [shared services](docs/SHARED_SERVICES.md), [workshop graph](docs/WORKSHOP_GRAPH.md) and [achievement design](docs/WORKSHOP_ACHIEVEMENT_DESIGN.md).
- [Logistics boundaries](docs/LOGISTICS_API_PROGRESS.md) and [progress](docs/FORGE_PROGRESS.md).

### Configuration and validation

Default logging is `INFO`; existing `UserData/MelonPreferences.cfg` settings take precedence. Set `DEBUG` explicitly and restart for diagnostics. Framework defaults are compiled into the DLL; content mods document their runtime configuration.

The 2026-10-06 candidate records machine processing, selected local logistics and save readback across processes. Mouse/keyboard UI, stock weights, built-in workshop rewards, two full nightly settlements, real legacy-terminal migration and missing-mod recovery/save-reload remain unvalidated. Recovery has offline checks and Release-build evidence. Later source does not inherit earlier binary results; check source, package and loaded versions separately. See source [progress](docs/FORGE_PROGRESS.md) for scope and [CHANGELOG](docs/RELEASE_CHANGELOG.md) for changes.

Released under the MIT License.

---

## 中文

作者：Nicokobo · 源码版本：**0.6.34** · 适用：**Probably Stolen Demo，Steam Build `25382790`**

Nicokobo Forge 为内容 Mod 提供通用注册、原生适配、机器模板、资源事务和 Nico 工坊，内置全域制造终端、全开局工坊名片与十项原版成就。各内容 Mod 维护自己的物品、配方、价格、成长和解锁页。

### 安装与更新

1. 安装 [MelonLoader](https://melonwiki.xyz/)，关闭游戏。
2. 将 `Nicokobo.Forge-0.6.34.dll` 和所需内容 Mod DLL 放入 `Mods/`。
3. 移出同程序集旧 DLL。多个包附带 Forge 时只保留一份配套文件，版本号文件名可保留。

Forge 可单独使用内置工坊与原版成就。

#### 可选兼容补丁

`Nicokobo.CompatibilityPatches-*.dll` 为独立可选组件，将 Wage's Perks **1.3.4** 的启动挂载延后至本地化初始化完成，避免过早读取文本导致崩溃。使用该版本时才复制到 `Mods/`，移出即可停用；Forge 不会自动安装或加载。目标缺失或版本不符时补丁自行停用并记录原因，不影响其他 Mod。 详见 `compatibility/README.md`。

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

### 开发与接入

框架默认值、限制、重试与工坊参数集中在 [BuildConfig](BuildConfig/README.md)，随 `Nicokobo.Forge.dll` 编译；内容 Mod 数值见[关联仓库 BuildConfig](../probably-stolen/mods-melonloader/BuildConfig/README.md)。动作前查看 `ForgeCapabilities.Current`，注册 `Accepted` 与目录 `Applied` 分开确认。

#### API 能力

| 能力 | 范围 |
| --- | --- |
| 注册与生命周期 | 物品、设施、节点、机器模组、效果；稳定 ID、所有者、冲突诊断、模组观察、文本接入、开局 ID 与周目数据。 |
| 机器模板 | 原版 UI、物品／液体输入、物品／容器输出、电池／模组／手册槽、额外批次与失败恢复。 |
| 价值与液体 | 液体组分、价值账本、生产材料价值及内容方机器生产倍率透传。 |
| 工坊与资源 | 独立标签、解锁星图、条件与回调；嵌入式图集发布／恢复、只读 ES3 解析、原生补丁目标检查。 |
| 库存 | 只读快照、单件搬运预检及单次搬运／倒液；`InventoryTransfer` 总能力仍为 `false`。 |

动态模组说明在原位置临时替换显示标签，绘制完成或异常时恢复，不改保存用基础说明；认知涡轮上限需 Forge 0.6.31。档案箱扩容适用新旧档，原位置保留；紧凑选择栏适用原版与内容 Mod 卡片。

终端稳定 ID 为 `nicokobo.forge.machines.universal_manufacturing_terminal`，迁移旧 `universal_manufacturing_terminal` 与 `mechanical_manufacturer`。正常保存将来源写入 `modData["nicokobo.forge.save.item_providers_v1"]`，缺失项返款为 `unitValue × unitCount`；待返还物品图位于 `modData["nicokobo.forge.save.recovered_items_v1"]`。返还离线检查包含原版读档顺序核对、物品图与空间预检。

G 查询支持真实 ID、材料类别、跨 Mod／分类、提供者备注及无结果提示；Forge 管理分页、拖动与释放保护。原版机器扩展通过 `ForgeRecipeGuideApi.RegisterRecipes` 提交，合成扩展提交四条玻璃回收展示。工坊只拦截窗口内下层输入，打开时清理名片拖拽选中，关闭保护至释放帧；窗口外开始的拖拽保留释放事件。

#### 供货接入

`NativeItemOptions.NpcTrade` 声明类别与最早天数，`Suppliers` 可限制小偷／杰克逊，默认不限制白天供货人；`IncludeInNightShop` 加入杰克逊原版随机模组／节点名额。`NpcTradeStock` 表示供货钩子已安装，`NativeItemOptions.NightShop` 保留独立上架条件。

28 条入口共同抽取原版与合格 Mod 商品，不保底追加、不改全局拾荒表，直接供货保留类别与堆叠数量。杰克逊只替换随机模组／节点名额；设备、钥匙卡、具名专用模组、大存储区、武器、工具、容器和文件保留。老拾荒客固定枪弹、加工肉商、固定供血者、阴谋论顾客与三类退休专业人员的指定供货保留原版。

名片 `nicokobo.forge.nico_card` 加入原生日用品掉落表并保留开局赠送；供货前检查柜台、背包和嵌套容器中的自有名片，持有时不刷新。内容 Mod 自行声明物品掉落与机器夜店上架；十二种最终义体只由所属 Mod 制造。

#### 编译与本地产物

在 Forge 仓库执行：

```powershell
.\scripts\Build-P0.ps1 -GameDir 'F:\SteamLibrary\steamapps\common\Probably Stolen Demo'
```

默认检查并编译核心与四个示例，`-IncludeProbes` 加入工坊／效果探针。在 `probably-stolen` 仓库执行：

```powershell
.\mods-melonloader\Build-ForgeMods.ps1 -NoInstall
```

`Build-ForgeMods.ps1` 不带 `-NoInstall` 时还会安装配套 Forge 与四个内容 Mod，清理旧 DLL；`Build-P0.ps1` 仅检查编译。环境、输出、日志和依赖见[开发说明](docs/DEVELOPMENT.md)。

#### 工坊素材

名片为深底铜框大 N、2×1 占格；标题栏使用 `nicokobo.com` 齿轮 Logo，下划线网址由默认浏览器打开。Logo 位于 `src/Nicokobo.Forge/Assets/Brand/nicokobo_logo.png`，名片源图与提示在 `src/Nicokobo.Forge/Assets/IconSources/`；`py scripts/export_workshop_card.py` 导出库存图标和设计预览。

#### 文档

- [文档索引](docs/README.md)：API、示例与历史证据。
- [API 职责](docs/API_BOUNDARIES.md)与[能力范围](docs/SCOPE.md)。
- [机器 API](docs/MACHINE_API.md)、[公共服务](docs/SHARED_SERVICES.md)、[工坊图谱](docs/WORKSHOP_GRAPH.md)与[成就方案](docs/WORKSHOP_ACHIEVEMENT_DESIGN.md)。
- [物流接入边界](docs/LOGISTICS_API_PROGRESS.md)与[当前进度](docs/FORGE_PROGRESS.md)。

### 配置与验证

默认日志为 `INFO`，已有 `UserData/MelonPreferences.cfg` 设置优先；详细诊断需显式设为 `DEBUG` 并重启。框架默认数值随 DLL 编译，内容 Mod 运行时配置见各自说明。

2026-10-06 候选记录了机器加工、部分本地物流与跨进程保存读回。鼠键 UI、供货权重、内置工坊奖励、完整两晚结算、真实旧终端迁移和卸载 Mod 后的返还／保存重载仍待验收；返还流程已有离线检查和 Release 编译证据。后续源码不继承旧二进制结论，源码、包内与实际加载版本分别核对。范围见源码 [当前进度](docs/FORGE_PROGRESS.md)，版本变化见 [CHANGELOG](docs/RELEASE_CHANGELOG.md)。

本项目以 MIT 许可证发布。
