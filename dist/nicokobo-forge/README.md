# Nicokobo Forge / 模组前置框架

作者：Nicokobo · 配套版本：**0.6.33** · 目标：**Probably Stolen Demo，Steam Build `25382790`**


## 中文

Nicokobo Forge 是内容 Mod 的公共前置，提供物品／效果注册、机器模板、材料／液体／电量事务和 Nico 工坊。框架内置全域制造终端、全开局名片与十项原版成就；具体配方、物品和玩法由内容 Mod 提供。


### 安装与更新

1. 安装 [MelonLoader](https://melonwiki.xyz/)，关闭游戏。
2. 将 `Nicokobo.Forge-0.6.33.dll` 与所需内容 Mod DLL 放入游戏 `Mods/`。
3. 移出同程序集的旧 DLL，每个程序集只保留一份。机核协议和物流网络都带 Forge 时只安装一份同次构建的文件，无需改名。

Forge 可单独使用内置工坊与原版成就。源码版本、包内版本和游戏实际加载版本分别确认。


### 可选：兼容补丁

`Nicokobo.CompatibilityPatches-0.1.3.dll` 是**可选**组件，不属于 Forge 本体，也不会被 Forge 自动安装或加载。它把 Wage's Perks **1.3.4** 的启动挂载延后到游戏本地化初始化完成之后，避免该 Mod 因过早读取本地化文本而崩溃。

只在这款第三方 Mod 已安装时才把该 DLL 复制到游戏 `Mods/`；不需要时不要复制，已经放入的移出即可停用。目标缺失或版本不符时补丁自身停用并记录原因，不影响 Forge 与其他内容 Mod。


### 全域制造终端

共享 K05 全域制造终端由 Forge 提供：外部 3×3，输入／输出仓各 9×6，模组仓沿用 7×5 环形定义；基础价值 400，基础耗电 25，每晚一批。合成扩展、机械飞升和物流网络分别追加配方；只安装 Forge 时没有制造配方。

终端在夜间商店生成库存时有 40% 上架机会，持有后仍可购买；购买不立即补货，货架满时跳过，不参加白天 NPC 供货。

读档支持旧合成扩展的两个已知终端 ID，保留物品身份、状态、占格与内部物品关系，后续正常保存使用当前 ID。真实旧档迁移仍待对应版本的游戏验证。


### 卸载内容 Mod 后的旧档

Forge 0.6.32 起只处理通过 Forge 注册且存档已有提供者记录的普通物品；原生登记缺失、当前无 Forge 声明，并确认提供者 DLL 已移除时才清理。未通过 Forge 注册的第三方物品、无记录旧档，以及提供者仍安装或无法确认来源的项保留。先在原内容 Mod 仍安装时用新版 Forge 正常保存，才建立来源记录；卸载时继续保留 Forge。旧制造终端继续迁移。

符合清理条件且属于玩家的缺失项按存档单价与数量返还信用点。缺失机器／容器里的正常材料、电池、模组和其他物品保持完整身份与状态；工厂可用且空间足够时返回店内、后房或储物容器，否则保存在本周目的待返还数据中，下次读档或正常保存前再尝试，不折算正常物品。已售、未持有或不可达物品不返款。

清理、余额和待返还数据随下一次原版正常保存写入，重复读档不累计返款。此流程需要保留 Forge；当前仅有离线检查与编译证据，真实旧档读取、返还及保存重载尚待原生验收。

### G 键合成表

鼠标指向物品按 **G**，查看它作为产物与材料的配方；指向机器时另列自身加工配方。指向另一件物品按 G 切换查询；同种物品、窗口或空白处再按 G 关闭。无目标时浏览完整已登记目录并保留当前场景内的阅读页，输入文字或打开工坊时不触发。

合成表由 Forge 独立提供，机器与追加配方自动加入；内容 Mod 可提交原版机器扩展的展示声明，实际加工和实体手册仍由对应提供者负责。只安装 Forge 时也能打开已登记的目录；本次窗口交互尚待原生验收。

### Nico 工坊

所有开局获赠名片，按 `N` 或双击名片打开。原版页有十项成就，分别记录进度、达成与领奖，每项每周目领取一次。背包放不下时保留资格；通关徽章不占库存。机械飞升提供独立伪人页，仅对应开局开放。

领取并保存确认“立业有成”后，本周目 NPC 收购预算×2，当前及后续顾客均生效；与伪人原有预算×2叠加时合计×4。已有有效领奖记录在载入确认后生效，新周目重新达成。


### 公共功能

- 原版档案箱内部扩为 16×16（256 格），保留已有内容与收纳规则。
- 新游戏选择栏采用紧凑行高，支持滚轮与滚动条。
- 一次性／高级加速器可立即加工 Forge 机器一批；成功后才消耗工具或充能。
- 手册从当前机器与追加配方目录读取材料、数量、分类及双语条件，内容更新后刷新。
- 支持每批动态选择产物，配套模组矩阵在 K02 添加随机稀有升级；分类、候选与价格由内容 Mod 维护，阅读配方不触发随机抽取。
- 白天供货按指定 NPC、类别、当前天数和实际持有筛选，合格 Mod 候选均分固定总权重 `0.25`，不保底追加；可显式让模组加入杰克逊夜间原版模组／节点名额。直接供货的原版结果权重为 `1`，Mod 总概率约 `20%`；原生表保留其他条目的权重。独立夜店上架条件继续生效。配套机核的矿工仍独立按原版矿石 70%、石英 15%、钛矿料 15% 整批选矿。
- 单件搬运与倒液核对两端实际变化；整批奖励用占格图预检落位。完整资源／电力网络由内容方实现。


### 配置与验证

默认日志 `INFO`，已有 `UserData/MelonPreferences.cfg` 设置优先；详细诊断需显式 `DEBUG` 并重启。框架数值随 DLL 编译，内容 Mod 的运行时配置见各自说明。

2026-10-06 候选记录了机器加工、部分本地物流和跨进程保存读回。鼠键 UI、供货权重、内置工坊奖励、完整两晚结算与真实旧终端迁移仍未完成验收；后续源码不自动继承较早二进制的结论。完整范围见源码 `docs/FORGE_PROGRESS.md`，版本变化见 [change.log](change.log)。

本项目以 MIT 许可证发布。


---


内容 Mod 可选择保留其 NPC 原生掉落表条目的原有权重，仍受类别、天数、持有与登记状态筛选；这些条目不再分摊普通供货的总权重0.25。直接供货与矿工独立抽取继续按各自规则处理。

## English

Author: Nicokobo · Matching version: **0.6.33** · Target: **Probably Stolen Demo, Steam Build `25382790`**

Nicokobo Forge is a shared content-mod dependency providing item/effect registration, machine templates, material/liquid/power transactions and the Nico Workshop. Forge owns the shared manufacturing terminal, an all-start card and ten native-game achievements. Content mods provide recipes, items and gameplay.


### Installation and updates

1. Install [MelonLoader](https://melonwiki.xyz/) and close the game.
2. Copy `Nicokobo.Forge-0.6.33.dll` and required content mod DLLs into `Mods/`.
3. Remove older copies of each assembly. Keep one matching Forge DLL when both Mechcore Protocol and Logistics Nexus include it; versioned filenames can stay unchanged.

Forge can be installed alone for its workshop and native achievements. Source, package and actually loaded versions must be checked separately.


### Optional: compatibility patches

`Nicokobo.CompatibilityPatches-0.1.3.dll` is an **optional** component. It is not part of Forge itself, and Forge never installs or loads it automatically. It defers the Wage's Perks **1.3.4** startup mounts until the game's localization initialization has completed, so that mod does not fail on early localization reads.

Copy the DLL into the game's `Mods/` folder only when that third-party mod is installed; remove it to disable the patches. When the target is missing or its version differs, the patch disables itself and logs the reason without affecting Forge or other content mods.


### Universal Manufacturing Terminal

Forge owns shared K05: a 3×3 footprint, separate 9×6 input/output grids, the native 7×5 ring-shaped module bay, base value 400 and base power 25. It processes one batch per night. Synthesis, Aug and Logistics Nexus contribute recipes; Forge alone supplies none.

The terminal has a 40% listing chance when night-shop stock is generated, including while owned. Purchases do not restock immediately; full shelves skip the item. It is excluded from daytime NPC supply.

Loading supports two known legacy Synthesis terminal IDs while preserving identity, state, shape and internal item relationships. Subsequent normal saves use the current ID. Real legacy-save migration still needs validation for the matching build.


### Missing content after uninstalling a mod

Forge 0.6.32 limits cleanup to ordinary items successfully registered through Forge with a saved provider record. Removal requires a missing native registration, no current Forge declaration and a confirmed absent provider DLL. Third-party items outside Forge registration, legacy saves without records and uncertain provider status remain untouched. Save normally with the original provider installed before uninstalling it, and keep Forge installed.

Eligible player-owned missing items refund their saved unit value times count. Intact contents of missing machines or containers preserve identity, state, charge, quality and nested contents; unavailable factories or inventory space keep their full graphs in run recovery data for later placement. Sold, unowned and unreachable items grant no refund. Inventory, balance and recovery data persist together on the next normal save; reloading an unchanged save does not accumulate refunds. Native old-save recovery and save/reload remain unverified.

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
- Dynamic outputs can select an item once per batch. Matrix contributes the random Rare upgrade to K02 and owns its pool, tiers and prices; reading does not draw an output.
- Daytime supply and opted-in Jackson night-module slots divide a fixed total Mod weight of 0.25 equally among eligible candidates after supplier, category, day and ownership filtering. Direct native results have weight 1, giving a combined Mod chance of about 20%; native tables retain their other entries' weights. Separate night-shop listings retain their availability rules. Matching Mechcore miner batches retain separate odds: native ore 70%, Quartz 15%, Titanium Ore 15%.
- Single-item transfers and pours verify changes at both ends; reward batches use a managed placement preflight. Content mods remain responsible for complete resource/power networks.


### Configuration and validation

Default logging is `INFO`; existing `UserData/MelonPreferences.cfg` settings take precedence. Set `DEBUG` explicitly and restart for detailed diagnostics. Framework defaults are compiled into the DLL; see each content mod for runtime configuration.

The dated 2026-10-06 candidate records machine processing, selected local logistics and save readback across processes. Mouse/keyboard UI, stock weights, built-in workshop rewards, two complete nightly settlements and real legacy-terminal migration remain unvalidated. Later source does not inherit earlier binary results. See source `docs/FORGE_PROGRESS.md` for scope and [change.log](change.log) for changes.

Released under the MIT License.

Content mods may retain their original native NPC loot-table weights after category, day, ownership and registration checks; these opted-in entries do not divide the ordinary 0.25 supply budget. Direct supply and separate miner draws retain their own rules.
