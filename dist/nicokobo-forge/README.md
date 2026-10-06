# Nicokobo Forge / 模组前置框架

作者：Nicokobo · 配套版本：**0.6.27** · 目标：**Probably Stolen Demo，Steam Build `25382790`**


## 中文

Nicokobo Forge 是内容 Mod 的公共前置，提供物品／效果注册、机器模板、材料／液体／电量事务和 Nico 工坊。框架内置全域制造终端、全开局名片与十项原版成就；具体配方、物品和玩法由内容 Mod 提供。


### 安装与更新

1. 安装 [MelonLoader](https://melonwiki.xyz/)，关闭游戏。
2. 将 `Nicokobo.Forge-0.6.27.dll` 与所需内容 Mod DLL 放入游戏 `Mods/`。
3. 移出同程序集的旧 DLL，每个程序集只保留一份。机核协议和物流网络都带 Forge 时只安装一份同次构建的文件，无需改名。

Forge 可单独使用内置工坊与原版成就。源码版本、包内版本和游戏实际加载版本分别确认。


### 全域制造终端

共享 K05 全域制造终端由 Forge 提供：外部 3×3，输入／输出仓各 9×6，模组仓沿用 7×5 环形定义；基础价值 400，基础耗电 25，每晚一批。合成扩展、机械飞升和物流网络分别追加配方；只安装 Forge 时没有制造配方。

终端在夜间商店生成库存时有 40% 上架机会，持有后仍可购买；购买不立即补货，货架满时跳过，不参加白天 NPC 供货。

读档支持旧合成扩展的两个已知终端 ID，保留物品身份、状态、占格与内部物品关系，后续正常保存使用当前 ID。真实旧档迁移仍待对应版本的游戏验证。


### Nico 工坊

所有开局获赠名片，按 `N` 或双击名片打开。原版页有十项成就，分别记录进度、达成与领奖，每项每周目领取一次。背包放不下时保留资格；通关徽章不占库存。机械飞升提供独立伪人页，仅对应开局开放。

领取并保存确认“立业有成”后，本周目 NPC 收购预算×2，当前及后续顾客均生效；与伪人原有预算×2叠加时合计×4。已有有效领奖记录在载入确认后生效，新周目重新达成。


### 公共功能

- 原版档案箱内部扩为 16×16（256 格），保留已有内容与收纳规则。
- 新游戏选择栏采用紧凑行高，支持滚轮与滚动条。
- 一次性／高级加速器可立即加工 Forge 机器一批；成功后才消耗工具或充能。
- 手册从当前机器与追加配方目录读取材料、数量、分类及双语条件，内容更新后刷新。
- 白天供货按指定 NPC、类别、当前天数和实际持有筛选，合格 Mod 候选均分固定总权重 `0.25`，不保底追加；可显式让模组加入杰克逊夜间原版模组／节点名额。直接供货的原版结果权重为 `1`，Mod 总概率约 `20%`；原生表保留其他条目的权重。独立夜店上架条件继续生效。配套机核的矿工仍独立按原版矿石 70%、石英 15%、钛矿料 15% 整批选矿。
- 单件搬运与倒液核对两端实际变化；整批奖励用占格图预检落位。完整资源／电力网络由内容方实现。


### 配置与验证

默认日志 `INFO`，已有 `UserData/MelonPreferences.cfg` 设置优先；详细诊断需显式 `DEBUG` 并重启。框架数值随 DLL 编译，内容 Mod 的运行时配置见各自说明。

2026-10-06 候选记录了机器加工、部分本地物流和跨进程保存读回。鼠键 UI、供货权重、内置工坊奖励、完整两晚结算与真实旧终端迁移仍未完成验收；后续源码不自动继承较早二进制的结论。完整范围见源码 `docs/FORGE_PROGRESS.md`，版本变化见 [CHANGELOG](CHANGELOG.md)。

本项目以 MIT 许可证发布。


---


## English

Author: Nicokobo · Matching version: **0.6.27** · Target: **Probably Stolen Demo, Steam Build `25382790`**

Nicokobo Forge is a shared content-mod dependency providing item/effect registration, machine templates, material/liquid/power transactions and the Nico Workshop. Forge owns the shared manufacturing terminal, an all-start card and ten native-game achievements. Content mods provide recipes, items and gameplay.


### Installation and updates

1. Install [MelonLoader](https://melonwiki.xyz/) and close the game.
2. Copy `Nicokobo.Forge-0.6.27.dll` and required content mod DLLs into `Mods/`.
3. Remove older copies of each assembly. Keep one matching Forge DLL when both Mechcore Protocol and Logistics Nexus include it; versioned filenames can stay unchanged.

Forge can be installed alone for its workshop and native achievements. Source, package and actually loaded versions must be checked separately.


### Universal Manufacturing Terminal

Forge owns shared K05: a 3×3 footprint, separate 9×6 input/output grids, the native 7×5 ring-shaped module bay, base value 400 and base power 25. It processes one batch per night. Synthesis, Aug and Logistics Nexus contribute recipes; Forge alone supplies none.

The terminal has a 40% listing chance when night-shop stock is generated, including while owned. Purchases do not restock immediately; full shelves skip the item. It is excluded from daytime NPC supply.

Loading supports two known legacy Synthesis terminal IDs while preserving identity, state, shape and internal item relationships. Subsequent normal saves use the current ID. Real legacy-save migration still needs validation for the matching build.


### Nico Workshop

Every start receives a card. Press `N` or double-click it. The native page tracks ten achievements with separate progress, completion and one-time rewards per run. Insufficient backpack space retains eligibility; the final badge uses no inventory slot. Aug supplies a separate page restricted to its start.

After claiming and confirming the native final achievement reward, NPC purchase budgets double for the current run, including current and future customers. This stacks with the existing Aug ×2 budget for a total ×4. Confirmed reward records apply after loading; new runs must qualify again.


### Shared features

- The native Dossier expands to 16×16 (256 cells), preserving contents and admission rules.
- Compact new-game rows support wheel scrolling and a scrollbar.
- Disposable/advanced accelerators process one immediate Forge batch and consume the tool or charge only after success.
- Handbooks read current machines and additional recipes, including inputs, counts, categories and bilingual conditions, and refresh after changes.
- Daytime supply and opted-in Jackson night-module slots divide a fixed total Mod weight of 0.25 equally among eligible candidates after supplier, category, day and ownership filtering. Direct native results have weight 1, giving a combined Mod chance of about 20%; native tables retain their other entries' weights. Separate night-shop listings retain their availability rules. Matching Mechcore miner batches retain separate odds: native ore 70%, Quartz 15%, Titanium Ore 15%.
- Single-item transfers and pours verify changes at both ends; reward batches use a managed placement preflight. Content mods remain responsible for complete resource/power networks.


### Configuration and validation

Default logging is `INFO`; existing `UserData/MelonPreferences.cfg` settings take precedence. Set `DEBUG` explicitly and restart for detailed diagnostics. Framework defaults are compiled into the DLL; see each content mod for runtime configuration.

The dated 2026-10-06 candidate records machine processing, selected local logistics and save readback across processes. Mouse/keyboard UI, stock weights, built-in workshop rewards, two complete nightly settlements and real legacy-terminal migration remain unvalidated. Later source does not inherit earlier binary results. See source `docs/FORGE_PROGRESS.md` for scope and [CHANGELOG](CHANGELOG.md) for changes.

Released under the MIT License.
