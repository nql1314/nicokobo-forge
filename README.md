# Nicokobo Forge / 模组前置框架

Author: Nicokobo · Source Version: **0.6.18** · Target: **Probably Stolen Demo，Steam Build `25382790`**

Nicokobo Forge 为内容 Mod 提供通用注册、原生适配和资源事务，并内置 Nico工坊的全开局名片与原版成就页。各内容 Mod 维护自己的物品、配方、价格、成长和独立解锁页。

## 安装

1. 游戏需已安装 [MelonLoader](https://melonwiki.xyz/)。
2. 将配套 `Nicokobo.Forge` DLL 与所需 Mod DLL 复制到游戏 `Mods/`；只使用原版成就时可单独安装 Forge。
3. 更新时移出旧 DLL，Forge 和每个内容 Mod 各只保留一份。使用同一次配套构建的文件。

此前配套：机械飞升 `0.1.39`、模组矩阵 `1.1.2`、物流脉络 `0.2.1`、合成扩展 `0.9.41`，机核协议合集版本 `0.0.3`。当前 Forge `0.6.18` 与合成扩展 `0.9.45` 的夜间额外批次改动只完成源码检查与编译，未打包或安装。源码版本和本地包不代表游戏当前已安装版本；构建、安装与实机验证范围见[当前进度](docs/FORGE_PROGRESS.md)。

## Nico工坊

所有开局由 Forge 发放 `nicokobo.forge.nico_card` 名片，按 N 或双击名片打开“原版”页。10 项成就分别显示进度、达成和领奖，奖励按周目一次性领取。满背包会延迟名片发放或保留领奖资格；通关徽章是展示标记，不占库存。安装机械飞升后另有“伪人：机械飞升”页，只在对应开局开放。条件与奖励见[成就方案](docs/WORKSHOP_ACHIEVEMENT_DESIGN.md)。

领取原版终局成就“立业有成”后，本周目所有 NPC 的收购预算×2，当前顾客和后续来访者均生效，成交正常扣款。已领取徽章的旧周目在载入并确认记录后自动获得效果；新周目需重新达成并领取。伪人原有预算×2继续叠加，合计×4。

名片采用深色底、铜色边框与大 N 标志，保留 2×1 占格；工坊标题栏采用 `nicokobo.com` 的齿轮 Logo，点击带下划线的网址会用系统默认浏览器打开网站。网站 Logo 原图嵌入 `src/Nicokobo.Forge/Assets/Brand/nicokobo_logo.png`。名片图标源图与生成提示保存在 `src/Nicokobo.Forge/Assets/IconSources/`，用 `py scripts/export_workshop_card.py` 导出库存图标及本地设计预览。

工坊只拦截窗口范围内的下层输入，窗口外继续操作。打开时清理激活名片留下的拖拽选中状态，关闭点击保护至鼠标释放当帧；从窗口外开始的库存拖拽保留自己的释放事件。

名片在正常初始化时加入原生日用品掉落表，保留所有开局的赠送入口。每次白天 NPC 供货／刷货前检查玩家实际持有，已有名片时不再刷新名片；检查包含柜台、背包及嵌套容器，只计算玩家自己的物品。内容 Mod 各自声明物品掉落；十二种最终义体仅由所属 Mod 制造，合成扩展的五台机器可在夜间商店生成库存时上架，持有后仍可购买。购买后不自动补货，货架放不下的新增商品跳过。

`NativeItemOptions.NpcTrade` 可声明白天供货的类别及相对权重，包括食品与医疗品；`ForgeCapabilities.Current.NpcTradeStock` 表示供货钩子已安装。当前接入矿工、废料商、拆解商、食品商、医药商、拾荒者、批发商、革命军和来访发明家等共 36 条供货入口。矿工每次只抽一种矿物，选中新增矿物时替换整批原版矿石，件数与随天数增长的规则由原版生成；其余入口按各自类别追加一件已应用物品，在第一件原版待售货物摆放前上柜台并读取结果。农夫、水商和供水的冰矿工沿用原版供货。每次实际供货和刷货重新抽取；购买、赠品和夜间商店不触发白天供货抽取。原生归属、阵营和柜台容量规则继续生效。未声明 `NpcTrade` 的物品不参加供货抽取。

## 通用能力

- 原版档案箱内部容量扩为 16×16（256 格），新游戏和已有存档共用；原有物品、位置及可收纳物品种类沿用原版。
- 物品、设施、节点、机器模组及效果注册，稳定 ID、所有者与冲突诊断。
- 公共生命周期、模组观察与文本接入，开局 ID 和周目数据承载。
- 新游戏选择栏自动使用紧凑行高，选项超出时支持滚轮和细滚动条；原版与内容 Mod 卡片共用。
- 原版机器 UI 模板、物品／液体输入、物品或容器输出、电池／模组／手册槽、默认每夜一批、内容方声明的额外批次及失败恢复。
- 液体组分与价值账本、生产材料价值计算，以及内容方声明的机器生产倍率透传。
- Nico 工坊的独立标签、解锁星图、条件展示与回调分派。
- 内容方嵌入式图集的发布与恢复、只读 ES3 文件解析、原生补丁入口检查。
- 库存只读快照与完整单件搬运预检；`InventoryTransfer` 当前为 `false`。

动作前查看 `ForgeCapabilities.Current`；注册 `Accepted` 与目录 `Applied` 是不同状态。具体契约见 [API 职责](docs/API_BOUNDARIES.md)和[能力范围](docs/SCOPE.md)。

## 开发与数值

框架自己的默认值、限制、重试及工坊参数集中在 [BuildConfig](BuildConfig/README.md)，随 `Nicokobo.Forge.dll` 编译。内容 Mod 的数值位于[关联仓库的 BuildConfig](../probably-stolen/mods-melonloader/BuildConfig/README.md)。

在 Forge 仓库执行：

```powershell
.\scripts\Build-P0.ps1 -GameDir 'F:\SteamLibrary\steamapps\common\Probably Stolen Demo'
```

默认检查并编译核心与四个示例；`-IncludeProbes` 加入工坊和效果探针。要构建、检查并打包四个内容 Mod，在 `probably-stolen` 仓库执行：

```powershell
.\mods-melonloader\Build-ForgeMods.ps1
```

默认生成本地文件。开发环境、输出位置、日志与依赖选择见[开发说明](docs/DEVELOPMENT.md)。框架默认日志为 `INFO`，用户 `MelonPreferences.cfg` 设置优先。

## 文档

- [文档索引](docs/README.md)：当前 API、示例和历史证据入口。
- [机器 API](docs/MACHINE_API.md)：注册、投料、液体、电量、生产价值和事务。
- [公共服务](docs/SHARED_SERVICES.md)：图集、ES3 读取、补丁目标检查与现有 Mod 迁移。
- [工坊图谱](docs/WORKSHOP_GRAPH.md)：链、前置、条件、奖励与内容方的交易职责。
- [物流接入边界](docs/LOGISTICS_API_PROGRESS.md)：已开放的读取／预检与待实现项。
- [当前进度](docs/FORGE_PROGRESS.md)：本地验证、未验收项与日期明确的探针记录。

## English

A shared dependency for Probably Stolen Demo content mods, targeting Steam Build `25382790`. Forge provides registration, lifecycle events, native machine templates, liquid and power transactions, production value support, and the Nico Workshop. It includes an all-start workshop card and ten native achievements with per-run rewards. Content mods own their gameplay definitions and separate progression pages.

Copy the matching Forge and content DLLs into `Mods/`, keeping one copy per assembly. Build with `scripts/Build-P0.ps1`; use `Build-ForgeMods.ps1` in the companion repository for all four content mods. Numeric defaults are compiled from [BuildConfig](BuildConfig/README.md). Inventory transfer remains unavailable. Build results and dated native probes have separate validation scopes.

Released under the [MIT License](LICENSE).
