# 2026-10-01 机器模板 API 本地验证

## 产物与范围

- Forge `0.4.0`；合成扩展 `0.7.0`；伪人：机械飞升 `0.1.7`。
- 编译引用 `F:\SteamLibrary\steamapps\common\Probably Stolen Demo` 的当前原生绑定；Steam app `4349200` 的清单确认 Build / TargetBuild `25382790`。
- 本轮未复制到游戏 `Mods/`，未启动或注入游戏，未修改存档。下方均为本地代码、领域检查、编译和包内读回证据。

新 API 由 `ForgeMachineDefinition` 声明原版 UI 模板、电池、模组仓、手册、物品输入仓和多个液体容器槽。每台机器只有一种物品仓／现有容器输出模板。混合输入在同一资源事务中扣除；输出仓容量不足或容器装不下时取消本批。液体可以按混合比例或指定组分扣除；电池支持每批、每件和动态计费。逐项读回、提交前核对、失败反向恢复、故障隔离和同夜去重均由框架负责。

K01–K05、原版熔炉玻璃回收和伪人提供的 7 条 K05 配方已整体改用新模型。K04 保留当前整批价值灌装规则，灌装由框架完成；产物工厂先把原版水瓶模板清空并读回。旧的固体／液体模式、生命周期进度和兼容成员已删除，不提供旧存档迁移。

自定义机器的源代码只安装 `PlayerStore.EndNight` 与 `LoadGame` 两个生命周期 Hook，槽位回调在本机窗口上绑定。只有声明原版熔炉扩展时才安装它的投料和生产两个 Hook；原版模块装入／移出仍走原生流程。不再 Hook 全局窗口创建和存档解码。

## 验证

执行统一入口：

```powershell
.\mods-melonloader\Build-ForgeMods.ps1 `
  -DistRoot D:\workzone\probably-stolen\_build\releases\forge-machine-templates-20261001
```

| 检查 | 结果 |
| --- | --- |
| Forge 模板注册、冻结、冲突、组合输入与输出限制 | 39 条新增断言通过 |
| Forge 液体舍入、组分、容量、耗电与逐阶段故障注入 | 119 条新增断言通过；故障写入后恢复、撤回抛错及日志抛错不阻断剩余恢复 |
| Forge 原有注册、日志与物流领域检查 | 通过；旧生命周期进度断言已删除 |
| Forge 核心与六个示例（含 MachineTemplates） | Release 编译通过 |
| 机械飞升 | 422 条领域检查，Release 编译通过 |
| 模块矩阵 | 89 条领域检查，Release 编译与 Obfuscar 打包通过 |
| 物流枢纽 | L0 21、资源 37、电力 27 条领域检查；Release 编译与 Obfuscar 打包通过 |
| 合成扩展 | 603 条领域检查，Release 编译与 Obfuscar 打包通过 |
| 四个关联包中的 Forge | 独立版本与 SHA-256 读回一致，均为 `0.4.0.0` |
| 两个仓库的 `git diff --check` | 通过 |

核心和内容编译无警告、无错误。领域检查项目仍显示既有 .NET 6 生命周期提示；本轮未变更目标框架。

## 本地产物

- 框架 ZIP：`D:\workzone\nicokobo-forge\dist\Nicokobo.Forge-0.4.0.zip`，由现有 `scripts/Pack-ModSite.ps1` 生成。
- 三个混淆机核包与机械飞升配套目录：`D:\workzone\probably-stolen\_build\releases\forge-machine-templates-20261001`。
- 包内版本、哈希和未验状态的结构化记录：上述目录的 `validation.json`。

| DLL | SHA-256 |
| --- | --- |
| Nicokobo.Forge 0.4.0 | `E5EB27529EC8E107621909A03A4E591795400DB91C025FEDBD4311DA8411B0A4` |
| Synthesis Expansion 0.7.0（混淆） | `54C392E511A4CDC4279AA61431E236CAEB44D812C85DDAC594EE12612373D832` |
| Aug Mechanical Ascension 0.1.7 | `CEC21464BF01078AAF5AFAC04C38D6F0AEC3CB2FFEA205669AE1A3260D2B4C47` |

## 待实机验收

本轮没有证明 Harmony 安装、各声明 `Applied`、窗口实际布局与拖放、游戏内材料／液体／电量变化或存档重载成功。需要用新建可丢弃档逐项验证：K01–K05 及熔炉配方、物品和液体同批输入、现有输出容器、仓满／容量不足／电量不足不扣资源、模组装入移出后的当前电耗、同夜去重，以及过夜保存后退出并重新读档的状态。纯液体与现有容器输出的声明另见可编译 MachineTemplates 示例。
