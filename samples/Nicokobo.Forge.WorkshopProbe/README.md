# Nico 工坊测试 Mod

此示例向 Forge 注册独立的“工坊测试”标签页，提供四项测试解锁：测试信号、中继器、信标、组合链路。中继器需要测试信号，组合链路需要中继器和信标。前置未满足时仍可选中条目，但“解锁”不可用。满足条件时可双击条目或点击“解锁”。

测试解锁只记录在当前游戏进程的当前周目内，不扣信用点、不消耗物品、不产生游戏效果，也不写入存档。切换到不同 `runID` 的周目会清空测试状态。

构建：

```powershell
dotnet build samples/Nicokobo.Forge.WorkshopProbe/Nicokobo.Forge.WorkshopProbe.csproj -c Release -p:GameDir='F:\SteamLibrary\steamapps\common\Probably Stolen Demo'
```

将生成的 `Nicokobo.Forge.WorkshopProbe.dll` 与最新 `Nicokobo.Forge.dll` 安装到游戏 `Mods/` 目录。示例输出目录可能含项目引用带来的 Forge DLL 副本，只安装一份最新 Forge DLL。
