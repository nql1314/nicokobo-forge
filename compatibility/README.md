# Nicokobo Compatibility Patches

统一名称：**Nicokobo Compatibility Patches（Nicokobo 兼容补丁合集）**。程序集为 `Nicokobo.CompatibilityPatches`，当前源码候选为 **0.1.7**，现有打包产物仍为 `Nicokobo.CompatibilityPatches-0.1.3.dll`。

所有兼容补丁维护在同一个[独立项目](Nicokobo.CompatibilityPatches/README.md)中，统一编译、打包为一个可选 MelonLoader Mod DLL。各项实现放在项目的 `Patches/<补丁名>/`，由统一入口调用，不编入 Forge 核心，也不随 Forge 默认构建安装。

该 DLL 会随独立 Forge 玩家包与机核协议发布包一并分发，但在发布说明中标为**可选**：Forge 和机核协议都不会安装或加载它，只有实际安装了对应第三方 Mod 时，才由玩家手动把它复制到游戏 `Mods/`。`scripts/Pack-ModSite.ps1` 默认取 `dist/compatibility/nicokobo-compatibility-patches/` 中已发布的 DLL 放入 Forge 包；`Build-MechcoreProtocol.ps1` 也按同一规则把它复制进 `dist/mechcore-protocol/`。没有打包产物时回退到本地 `bin/Release` 构建，两者都不存在才警告并跳过，此时发布说明不出现该可选章节。

| 补丁 | 适用版本 | 作用 |
| --- | --- | --- |
| WagePerksStartup | Wage's Perks 1.3.4；Probably Stolen Demo 0.46D | 将四类读取本地化文本的原生挂载延后到游戏本地化初始化成功完成 |
| WagePerksDestinyDice | Wage's Perks 1.3.4；Probably Stolen Demo 0.46D | 补齐命运骰子的物品目录注册，让原生读档能重建已保存的骰子 |
| PCExpansionScalars | 深空装机 2.6；Probably Stolen Demo 0.46D | 规范旧属性名与 IL2CPP backing-field 别名，保留储物恢复和维修的完整校验，不改写原记录 |

目标 Mod 未安装或版本不匹配时，仅停用对应补丁。后续兼容修复继续加入这个项目和 DLL，不改写第三方 Mod。

装机字段兼容不与旧 `PCExpansionLegacyFix` 叠加，检测到时该项停用并提示。当前本地归档中的酿酒 3.6.2 已使用三参数充电接口，岁物品包 0.7 也没有报告中的旧 `PlaceIntoFurnace` 入口，本次未照搬对应旧补丁。Forge 数量桥接由 Forge 自身处理，合集不拦截或摘除数量钩子。前轮 0.1.6 功能候选已完成 74 项离线检查，并在 27-Mod 隔离组合中验证装机旧字段恢复与维修回读。0.1.7 整理发布标识与 change.log，原生证据保留实际受测版本；未重新打包或安装。

开局声望与物品身份校验修复已迁入伪人 0.1.52，由其自身精确应用配置差值并只验证自己的脑机接口。合集 0.1.4 已移除旧 `AugOpeningReputation` 与 `AugOpeningInventory`，更新时应配套更新伪人，避免旧的妙妙箱白名单覆盖新逻辑。
