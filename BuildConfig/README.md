# Forge 编译内置数值

[ForgeNumbers.cs](ForgeNumbers.cs) 集中维护框架自己的数字参数，随 `Nicokobo.Forge.dll` 编译发布。修改源文件后重新构建即可，无需新的设置菜单、MelonPreferences 条目或外部 JSON。

| 分组 | 内容 |
| --- | --- |
| `Assets` | 原生图集缓存未就绪时的重试间隔 |
| `SaveReadback` | 公共 ES3 读取的默认大小与深度；调用者可显式使用自己的上限 |
| `NpcStock` | 普通供货 Mod 总权重 `ModSupplyWeight = 0.25`，按筛选后候选种类数均分；直接供货原版结果权重 `NativeSupplyWeight = 1`，Mod 总概率约 20%。原生表保留其他条目的权重。矿工独立原版矿石权重为 7/3，原矿使用 `MinerWeight`（未声明则用 `Weight`），两种扩展矿石各为 0.5 时仍为原版 70%、石英和钛矿料各 15% |
| `Achievements` | Forge 内置原版成就的阈值、血清奖励数量、通关后的 NPC 收购预算倍率、刷新与发放重试、存档大小和奖励数量限制 |
| `Machines` | 默认电池／模组舱尺寸、默认产量、仓库边长和批次产量上限、液体输入槽与生产倍率上限、布局间距、生命周期优先级 |
| `ManufacturingTerminal` | Forge 内置全域制造终端的外部占格、仓库尺寸、基础价值、电耗、默认生产增值率、掉落权重与夜间上架概率 |
| `Inventory` | 档案箱内部网格尺寸（16×16）；持有数量只读快照最长复用时间（250 毫秒）；直接库存与全周目遍历、子物品、垃圾遍历和价值读取的上限 |
| `RunData` | 周目 JSON 长度和嵌套深度 |
| `StartSelection` | 新游戏选择栏的行高上下限、图标尺寸、图文间距、滚动条宽度和边距 |
| `Workshop` | 标题、标签页、条目和分组上限，刷新／切换间隔，界面尺寸、缩放、分页、节点尺寸和字号 |
| `Diagnostics` | 拖拽诊断槽位与错误日志的次数上限 |
| `NativeUnits` | 原生液体组分与毫升／信用点的换算契约，须与原生表示一致 |

核心工程和领域检查工程都直接编译这份源文件。框架不会引用内容 Mod 仓库；内置原版成就单独维护其阈值和奖励，全域制造终端维护本体参数，制造配方、成品价格、材料数量、效果和内容奖励由各内容方持有。机核协议三个组件的集中入口见 [Mod 编译内置数值](../../probably-stolen/mods-melonloader/BuildConfig/README.md)，独立物流的制造成本在其 `Integration/Content.cs`。

在 Forge 仓库执行：

```powershell
.\scripts\Build-P0.ps1 -GameDir 'F:\SteamLibrary\steamapps\common\Probably Stolen Demo' -IncludeProbes
```

关联检查与本地打包使用 `probably-stolen/mods-melonloader/Build-ForgeMods.ps1 -NoInstall`；省略 `-NoInstall` 会安装游戏 DLL 并清理旧版本。入口和输出见[开发说明](../docs/DEVELOPMENT.md)。编译和领域检查不代表原生运行或存档重载已经验收。
