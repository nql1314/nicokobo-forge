# 2026-10-01 液体价值补丁启动修复

游戏：Probably Stolen Demo 0.46D，Steam Build 25382790；MelonLoader 0.7.3。

## 原因与改动

21:26 用户日志记录 `nicokobo.forge.liquid_value` 的 `TargetInvocationException` 和回滚失败，随后关闭机器加工与合成扩展熔炉，主菜单 `PerkUIController.OnChange` 报 `Attempting to use an invalid operation handle`。

21:52 诊断启动记录实际失败目标为 `WaterFeatureHelper.UpdateWaterFeatureFake`。内部异常为 `WaterFeatureHelper` 原生静态初始化的 `SEHException`；Unity 日志显示该初始化经 `LocHelper.Get` 同步读取本地化文本并触发资源句柄异常。此时 Unity 资源提供器尚未就绪；该类型初始化失败会留在当前进程中。

Forge 0.6.2 删除启动时对 `WaterFeatureHelper.UpdateWaterFeatureFake` 和 `InitWaterFeature` 的两处补丁。容器 `TransferLiquid`、`AddLiquid`、`EmptyContainer` 仍同步价值账本，`GameItem.GetRefreshedValue` 读取时恢复保存价值。通用补丁日志补充失败目标、异常链与堆栈。

游戏安装目录仍保留 schema 5 配方；备份后替换为当前 schema 6 默认配方，没有新增自动迁移代码或改动配方定义。

## 检查与安装

- `scripts/Build-P0.ps1 -GameDir <当前游戏目录> -DefaultLogLevel INFO -IncludeProbes` 通过：49 项元数据签名、机器模板 45、批次恢复 119、价值账本 25、运行边界 88 项断言，以及核心和六个示例编译。
- 已安装 `Mods/Nicokobo.Forge-0.6.2.dll`；旧 DLL 移出 Mods，目录只留一个 Forge。新 DLL 与编译产物 SHA-256 均为 `24092314630AEE2AB9A06D072C20BBFF8511F2E1E23C9684B7AFD7F317B1210C`。
- 四个内容调用者重新编译，三个机核协议包经 `Build-MechcoreProtocol.ps1` 的 Release／Obfuscar 入口生成；机械飞升包也同步引用 0.6.2。四包内 Forge 与已安装 DLL 哈希一致，Forge 独立发布 ZIP 已生成。本轮仅替换游戏中的 Forge 与配方配置，内容 Mod 安装文件继续使用本次启动验证的原有版本。
- 21:56 新进程通过批处理主菜单启动检查。MelonLoader 与 Unity 启动日志没有上述异常或机器加工停用消息；合成扩展读取 20 条配方，熔炉报告 `extension installed; recipes=4; hooks=2`。
- 原有八个 `save_*.es3`、`SaveFile.es3`、`saves_index.es3` 共十个文件的最终 SHA-256 与启动前一致；启动改变的全局设置已恢复。`MelonPreferences.cfg` 与备份一致，Forge 的用户 `WARN` 覆盖保持不变。
- 诊断与复验进程均已结束。日志、原 DLL、配方和设置备份位于 `D:/workzone/probably-stolen/_build/game-error-fix-20261001-c7f3bc/`。

## 验收边界

本轮验证启动异常及相关适配器重新加载，未载入玩家存档或执行手动原生测试。机器实际过夜产出、液体价值倾倒／稀释／消耗，以及玩家存档保存重载均不在本次验收范围内。ModManager 的线上详情缺失警告仍存在，与此次启动异常分开处理。
