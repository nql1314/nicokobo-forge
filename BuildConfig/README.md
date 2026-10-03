# Forge 编译内置数值

[ForgeNumbers.cs](ForgeNumbers.cs) 集中维护框架自己的数字参数，随 `Nicokobo.Forge.dll` 编译发布。修改源文件后重新构建即可，无需新的设置菜单、MelonPreferences 条目或外部 JSON。

| 分组 | 内容 |
| --- | --- |
| `Assets` | 原生图集缓存未就绪时的重试间隔 |
| `SaveReadback` | 公共 ES3 读取的默认大小与深度；调用者可显式使用自己的上限 |
| `Achievements` | Forge 内置原版成就的阈值、血清奖励数量、刷新与发放重试、存档大小和奖励数量限制 |
| `Machines` | 默认电池／模组舱尺寸、默认产量、仓库边长和批次产量上限、液体输入槽与生产倍率上限、布局间距、生命周期优先级 |
| `Inventory` | 直接库存与全周目遍历、子物品、垃圾遍历和价值读取的上限 |
| `RunData` | 周目 JSON 长度和嵌套深度 |
| `Shop` | 补货等待、重试间隔和替换尝试次数 |
| `Workshop` | 标题、标签页、条目和分组上限，刷新／切换间隔，界面尺寸、缩放、分页、节点尺寸和字号 |
| `Diagnostics` | 拖拽诊断槽位与错误日志的次数上限 |
| `NativeUnits` | 原生液体组分与毫升／信用点的换算契约，须与原生表示一致 |

核心工程和领域检查工程都直接编译这份源文件。框架不会引用内容 Mod 仓库；内置原版成就单独维护其阈值和奖励，其余物品价格、材料数量、效果和奖励由各内容方持有。四个内容 Mod 的集中入口见 [Mod 编译内置数值](../../probably-stolen/mods-melonloader/BuildConfig/README.md)。

在 Forge 仓库执行：

```powershell
.\scripts\Build-P0.ps1 -GameDir 'F:\SteamLibrary\steamapps\common\Probably Stolen Demo' -IncludeProbes
```

关联构建与本地打包使用 `probably-stolen/mods-melonloader/Build-ForgeMods.ps1`。本次数值提取不改变公开 API 的参数值和当前默认行为；编译和领域检查不代表安装、原生运行或存档重载已经验收。
