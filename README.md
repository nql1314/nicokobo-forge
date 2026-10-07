# Nicokobo Forge / 模组前置框架

Author: Nicokobo · Source Version: **0.6.32** · Target: **Probably Stolen Demo，Steam Build `25382790`**


## 中文

Nicokobo Forge 为内容 Mod 提供通用注册、原生适配和资源事务，并内置全域制造终端、Nico工坊的全开局名片与原版成就页。各内容 Mod 维护自己的物品、配方、价格、成长和独立解锁页。


## 安装

1. 游戏需已安装 [MelonLoader](https://melonwiki.xyz/)。
2. 将配套 `Nicokobo.Forge` DLL 与所需 Mod DLL 复制到游戏 `Mods/`；只使用原版成就时可单独安装 Forge。
3. 更新时移出旧 DLL，Forge 和每个内容 Mod 各只保留一份。使用同一次配套构建的文件。
4. 独立 Forge 包与机核协议发布包另行附带的 `Nicokobo.CompatibilityPatches-*.dll` 为**可选**组件，不属于 Forge 本体，只在实际安装了对应第三方 Mod 时手动复制到 `Mods/`；Forge 和机核协议都不会自动安装或加载它。详见 `compatibility/README.md`。

源码版本和本地包不代表游戏当前已安装版本。内容组件使用同一次构建的 Forge；构建、安装与实机验证范围见[当前进度](docs/FORGE_PROGRESS.md)，旧配套版本保留在对应日期的记录中。


## 全域制造终端

终端物品、图标、售价、槽位、基础电耗和供货由 Forge 持有，稳定 ID 为 `nicokobo.forge.machines.universal_manufacturing_terminal`。默认外部占格 3×3，输入／输出仓 9×6，基础价值 400，耗电 25，每晚一批；不安装内容 Mod 时没有制造配方。合成扩展和机械飞升各自向同一终端追加配方，不再注册终端物品。

读档时，Forge 将旧合成扩展的 `universal_manufacturing_terminal` 与更早的 `mechanical_manufacturer` ID 转到当前终端，保留原物品身份、状态、占格和子物品槽位关系；下次正常保存写入新 ID。没有直接改写存档文件。迁移逻辑已通过离线检查，实际游戏读档、过夜及保存重载仍待验收。接入方法见[机器 API](docs/MACHINE_API.md)。


## 缺失物品读档兼容

Forge 0.6.32 将清理范围限定为**通过 Forge 注册 API 成功登记、且存档已有提供者记录的普通物品**。正常保存将物品 ID 与提供者程序集写入周目 `modData["nicokobo.forge.save.item_providers_v1"]`；读档时只有原生目录不再包含该 ID、当前没有 Forge 登记声明，并且确认提供者 DLL 已移除，才清理该物品。玩家实际持有的缺失项按存档 `unitValue × unitCount` 加回信用点；已售、未持有或不可达条目不返款。

未通过 Forge 注册的第三方物品不纳入清理。没有提供者记录的旧档先保留；在原内容 Mod 仍安装时用新版 Forge 正常保存，才建立相应记录。提供者仍安装但加载／注册失败、安装目录无法完整读取，或物品当前已有登记时均保留。旧制造终端 ID 继续沿用已有迁移。

缺失机器或容器中的正常物品保持原身份、数量、状态、充能、品质及内部物品，读档结束后尝试放回店内、后房和玩家储物容器。空间不足时，完整物品图留在当前周目 `modData["nicokobo.forge.save.recovered_items_v1"]`，下次读档或正常保存前再尝试，不把正常物品折算成钱。清理后的库存、余额与待返还数据在下一次原版正常保存中一并写入；不直接写存档文件，重复读档不累加返款。

卸载通过 Forge 登记物品的内容 Mod 时仍须保留配套 Forge，并先完成带提供者记录的正常保存。当前已做原版读档顺序静态核对、离线图与空间预检以及 Release 编译；真实旧档读取、原样返还和保存重载仍待原生验收。

## G 键合成表

G 键合成表由 Forge 提供。鼠标指向物品按 **G**，分别显示它作为产物和材料的配方，保留实际材料数量、条件和提供者备注；支持真实物品 ID、材料类别及跨 Mod／跨分类查询。鼠标指向机器时，还显示该机器的加工配方。指向另一件物品再按 G 可切换；指向同种物品、窗口或空白处按 G 关闭。无目标时浏览完整已登记配方目录，并记住当前场景内的阅读页；没有结果时显示提示，输入文字或打开 Nico工坊时不触发。

窗口、分页、拖动、鼠标释放保护与查询逻辑均由 Forge 持有，不依赖合成扩展。Forge 机器与追加配方自动进入目录；原版机器上的扩展配方由所属 Mod 通过 `ForgeRecipeGuideApi.RegisterRecipes` 提交展示声明，生产逻辑仍由原提供者持有。合成扩展提交四条玻璃回收配方，自己的实体手册继续由合成扩展提供。本次窗口交互尚未原生验证，接口见[机器 API](docs/MACHINE_API.md)。


## Nico工坊

所有开局由 Forge 发放 `nicokobo.forge.nico_card` 名片，按 N 或双击名片打开“原版”页。10 项成就分别显示进度、达成和领奖，奖励按周目一次性领取。满背包会延迟名片发放或保留领奖资格；通关徽章是展示标记，不占库存。安装机械飞升后另有“伪人：机械飞升”页，只在对应开局开放。条件与奖励见[成就方案](docs/WORKSHOP_ACHIEVEMENT_DESIGN.md)。

领取原版终局成就“立业有成”后，本周目所有 NPC 的收购预算×2，当前顾客和后续来访者均生效，成交正常扣款。已领取徽章的旧周目在载入并确认记录后自动获得效果；新周目需重新达成并领取。伪人原有预算×2继续叠加，合计×4。

名片采用深色底、铜色边框与大 N 标志，保留 2×1 占格；工坊标题栏采用 `nicokobo.com` 的齿轮 Logo，点击带下划线的网址会用系统默认浏览器打开网站。网站 Logo 原图嵌入 `src/Nicokobo.Forge/Assets/Brand/nicokobo_logo.png`。名片图标源图与生成提示保存在 `src/Nicokobo.Forge/Assets/IconSources/`，用 `py scripts/export_workshop_card.py` 导出库存图标及本地设计预览。

工坊只拦截窗口范围内的下层输入，窗口外继续操作。打开时清理激活名片留下的拖拽选中状态，关闭点击保护至鼠标释放当帧；从窗口外开始的库存拖拽保留自己的释放事件。

名片在正常初始化时加入原生日用品掉落表，保留所有开局的赠送入口。每次白天 NPC 供货／刷货前检查玩家实际持有，已有名片时不再刷新名片；检查包含柜台、背包及嵌套容器，只计算玩家自己的物品。内容 Mod 各自声明物品掉落；十二种最终义体仅由所属 Mod 制造，合成扩展的五台机器与 Forge 的全域制造终端可在夜间商店生成库存时上架，持有后仍可购买。购买后不自动补货，货架放不下的新增商品跳过。

`NativeItemOptions.NpcTrade` 声明白天供货类别与最早天数；`Suppliers` 可限制为小偷／杰克逊，默认不限制白天供货人，`IncludeInNightShop` 让模组候选也加入杰克逊夜间模组／节点名额。`NpcTradeStock` 表示供货钩子已安装。36 条供货入口将合格 Mod 商品与原版候选共同抽取，不保底追加商品，也不改写全局拾荒表。普通供货及夜间模组池中 Mod 总权重固定为 `0.25`，按筛选后的候选种类数平均分配；直接供货的原版结果权重为 `1`，Mod 总概率约为 `20%`，原生表抽取则保留原版条目的权重。独立的 `NativeItemOptions.NightShop` 上架条件与矿工单批选矿规则继续生效。完整契约见[能力范围](docs/SCOPE.md)。


## 通用能力

- 原版档案箱内部容量扩为 16×16（256 格），新游戏和已有存档共用；原有物品、位置及可收纳物品种类沿用原版。
- 物品、设施、节点、机器模组及效果注册，稳定 ID、所有者与冲突诊断。
- 公共生命周期、模组观察与文本接入，开局 ID 和周目数据承载。
- 动态模组说明在原有面板位置显示；绘制期间临时替换显示标签，结束或异常时恢复，不改写保存用的基础说明。认知涡轮的生效上限需要配套 Forge 0.6.31。
- 新游戏选择栏自动使用紧凑行高，选项超出时支持滚轮和细滚动条；原版与内容 Mod 卡片共用。
- 原版机器 UI 模板、物品／液体输入、物品或容器输出、电池／模组／手册槽、默认每夜一批、内容方声明的额外批次及失败恢复。
- 原版一次性／高级加速器可立即加工 Forge 机器一批，成功后才消耗加速器或充能；缺料、缺电、输出不足时保留工具。
- 配方手册的动态只读目录：新机器和追加配方自动加入，内容 Mod 提供分类与双语条件，阅读界面按目录版本刷新。
- 液体组分与价值账本、生产材料价值计算，以及内容方声明的机器生产倍率透传。
- Nico 工坊的独立标签、解锁星图、条件展示与回调分派。
- 内容方嵌入式图集的发布与恢复、只读 ES3 文件解析、原生补丁入口检查。
- 库存只读快照、完整单件搬运预检，以及独立的单次物品搬运／倒液接口；`InventoryTransfer` 总能力仍为 `false`，资源／电力网络由内容方实现。

动作前查看 `ForgeCapabilities.Current`；注册 `Accepted` 与目录 `Applied` 是不同状态。具体契约见 [API 职责](docs/API_BOUNDARIES.md)和[能力范围](docs/SCOPE.md)。


内容 Mod 可选择保留其 NPC 原生掉落表条目的原有权重，仍受类别、天数、持有与登记状态筛选；这些条目不再分摊普通供货的总权重0.25。直接供货与矿工独立抽取继续按各自规则处理。

## 开发与数值

框架自己的默认值、限制、重试及工坊参数集中在 [BuildConfig](BuildConfig/README.md)，随 `Nicokobo.Forge.dll` 编译。内容 Mod 的数值位于[关联仓库的 BuildConfig](../probably-stolen/mods-melonloader/BuildConfig/README.md)。

在 Forge 仓库执行：

```powershell
.\scripts\Build-P0.ps1 -GameDir 'F:\SteamLibrary\steamapps\common\Probably Stolen Demo'
```

默认检查并编译核心与四个示例；`-IncludeProbes` 加入工坊和效果探针。要构建、检查并打包四个内容 Mod，在 `probably-stolen` 仓库执行：

```powershell
.\mods-melonloader\Build-ForgeMods.ps1 -NoInstall
```

此示例检查并打包本地产物。`Build-ForgeMods.ps1` 不带 `-NoInstall` 时会安装配套 DLL 到游戏 `Mods/` 并清理同程序集旧文件；`Build-P0.ps1` 仅检查和编译。开发环境、输出位置、日志与依赖选择见[开发说明](docs/DEVELOPMENT.md)。框架默认日志为 `INFO`，用户 `MelonPreferences.cfg` 设置优先。


## 文档

- [文档索引](docs/README.md)：当前 API、示例和历史证据入口。
- [机器 API](docs/MACHINE_API.md)：注册、投料、液体、电量、生产价值和事务。
- [公共服务](docs/SHARED_SERVICES.md)：图集、ES3 读取、补丁目标检查与现有 Mod 迁移。
- [工坊图谱](docs/WORKSHOP_GRAPH.md)：链、前置、条件、奖励与内容方的交易职责。
- [物流接入边界](docs/LOGISTICS_API_PROGRESS.md)：已开放的读取／预检与待实现项。
- [当前进度](docs/FORGE_PROGRESS.md)：本地验证、未验收项与日期明确的探针记录。


---


## English

Author: Nicokobo · Matching version: **0.6.32** · Target: **Probably Stolen Demo, Steam Build `25382790`**

Nicokobo Forge is a shared content-mod dependency providing item/effect registration, machine templates, material/liquid/power transactions and the Nico Workshop. Forge owns the shared manufacturing terminal, an all-start card and ten native-game achievements. Content mods provide recipes, items and gameplay.


### Installation and updates

1. Install [MelonLoader](https://melonwiki.xyz/) and close the game.
2. Copy `Nicokobo.Forge-0.6.32.dll` and required content mod DLLs into `Mods/`.
3. Remove older copies of each assembly. Keep one matching Forge DLL when both Mechcore Protocol and Logistics Nexus include it; versioned filenames can stay unchanged.
4. The standalone Forge package and the Mechcore Protocol bundle also carry `Nicokobo.CompatibilityPatches-*.dll`, an **optional** component that is not part of Forge itself. Copy it into `Mods/` only when the matching third-party mod is installed; neither Forge nor the bundle installs or loads it. See `compatibility/README.md`.

Forge can be installed alone for its workshop and native achievements. Source, package and actually loaded versions must be checked separately.


### Universal Manufacturing Terminal

Forge owns shared K05: a 3×3 footprint, separate 9×6 input/output grids, the native 7×5 ring-shaped module bay, base value 400 and base power 25. It processes one batch per night. Synthesis, Aug and Logistics Nexus contribute recipes; Forge alone supplies none.

The terminal has a 40% listing chance when night-shop stock is generated, including while owned. Purchases do not restock immediately; full shelves skip the item. It is excluded from daytime NPC supply.

Loading supports two known legacy Synthesis terminal IDs while preserving identity, state, shape and internal item relationships. Subsequent normal saves use the current ID. Real legacy-save migration still needs validation for the matching build.


### Missing content after uninstalling a mod

Forge 0.6.32 limits cleanup to ordinary items successfully registered through Forge with a provider assembly recorded in the save. Normal saves write that map to `modData["nicokobo.forge.save.item_providers_v1"]`. Cleanup requires a missing native registration, no current Forge declaration and a confirmed absent provider DLL. Third-party items outside Forge registration and legacy saves without provider records remain untouched. An installed provider that fails to load/register, or an incomplete DLL identity scan, also retains its items. Save normally with the provider installed before uninstalling it, and keep Forge installed.

Eligible player-owned missing items refund saved unit value times count. Intact contents of missing machines or containers retain identity, state, charge, quality and nested contents; unavailable factories or inventory space keep their full graphs in the run recovery data for later placement. Sold, unowned and unreachable items grant no refund. Inventory, balance and recovery data persist together on the next normal save; reloading an unchanged save does not accumulate refunds. Native old-save recovery and save/reload remain unverified.

### G recipe browser

Hover an item and press **G** to view recipes that produce or consume it; machines also show their own recipes. Another item switches the query. The same kind, window or empty space closes it. With no target, browse the registered catalog at the last scene-local page. Typing and the workshop suppress the shortcut.

Forge provides this browser independently. Machine/additional recipes appear automatically; content mods may declare presentation for native-machine extensions while retaining processing and physical books. Forge alone can open its registered catalog. Native window interaction remains unverified.

### Nico Workshop

Every start receives a card. Press `N` or double-click it. The native page tracks ten achievements with separate progress, completion and one-time rewards per run. Insufficient backpack space retains eligibility; the final badge uses no inventory slot. Aug supplies a separate page restricted to its start.

After claiming and confirming the native final achievement reward, NPC purchase budgets double for the current run, including current and future customers. This stacks with the existing Aug ×2 budget for a total ×4. Confirmed reward records apply after loading; new runs must qualify again.


### Shared features

- The native Dossier expands to 16×16 (256 cells), preserving contents and admission rules.
- Compact new-game rows support wheel scrolling and a scrollbar.
- Disposable/advanced accelerators process one immediate Forge batch and consume the tool or charge only after success.
- Handbooks read current machines and additional recipes, including inputs, counts, categories and bilingual conditions, and refresh after changes.
- Daytime supply and opted-in Jackson night-module slots divide a fixed total Mod weight of 0.25 equally among eligible candidates after supplier, category, day and ownership filtering. `NpcTrade.Suppliers` can restrict daytime suppliers; `IncludeInNightShop` explicitly opts modules into native night module/node draws. Direct native results have weight 1, giving a combined Mod chance of about 20%; native tables retain their other entries' weights. Separate night-shop listings retain their availability rules. Matching Mechcore miner batches retain separate odds: native ore 70%, Quartz 15%, Titanium Ore 15%.
- Single-item transfers and pours verify changes at both ends; reward batches use a managed placement preflight. Content mods remain responsible for complete resource/power networks.


### Configuration and validation

Default logging is `INFO`; existing `UserData/MelonPreferences.cfg` settings take precedence. Set `DEBUG` explicitly and restart for detailed diagnostics. Framework defaults are compiled into the DLL; see each content mod for runtime configuration.

The dated 2026-10-06 candidate records machine processing, selected local logistics and save readback across processes. Mouse/keyboard UI, stock weights, built-in workshop rewards, two complete nightly settlements and real legacy-terminal migration remain unvalidated. Later source does not inherit earlier binary results. See source `docs/FORGE_PROGRESS.md` for scope and [CHANGELOG](docs/RELEASE_CHANGELOG.md) for changes.

Released under the MIT License.


### Development

Run scripts/Build-P0.ps1 with GameDir to check and compile Forge and four examples; -IncludeProbes adds workshop/effect probes. In the companion repository, Build-ForgeMods.ps1 -NoInstall checks and builds local packages. Omitting that switch installs Forge and all four content mods. See [API responsibilities](docs/API_BOUNDARIES.md), [scope](docs/SCOPE.md), [machine API](docs/MACHINE_API.md), [shared services](docs/SHARED_SERVICES.md), [workshop graph](docs/WORKSHOP_GRAPH.md), [development](docs/DEVELOPMENT.md) and [progress](docs/FORGE_PROGRESS.md).

Content mods may retain their original native NPC loot-table weights after category, day, ownership and registration checks; these opted-in entries do not divide the ordinary 0.25 supply budget. Direct supply and separate miner draws retain their own rules.
