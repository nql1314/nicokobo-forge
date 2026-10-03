# Forge 文档索引

目标 Demo Steam Build `25382790`。源码版本、配套组件和验证状态统一见[当前进度](FORGE_PROGRESS.md)。

| 文档 | 用途 |
| --- | --- |
| [SCOPE.md](SCOPE.md) | 框架能力、内容方职责与能力门控 |
| [API_BOUNDARIES.md](API_BOUNDARIES.md) | 按功能选用公共 API，事件与原生接入契约 |
| [SHARED_SERVICES.md](SHARED_SERVICES.md) | 图集、ES3 读取、原生补丁检查及现有 Mod 迁移 |
| [DEVELOPMENT.md](DEVELOPMENT.md) | 构建、关联项目、源码结构与验证方式 |
| [MACHINE_API.md](MACHINE_API.md) | 机器模板、配方、液体、电量、价值与失败恢复 |
| [WORKSHOP_GRAPH.md](WORKSHOP_GRAPH.md) | Nico 工坊图谱、条件、奖励和解锁回调 |
| [LOGISTICS_API_PROGRESS.md](LOGISTICS_API_PROGRESS.md) | 物流对通用 API 的使用与未开放能力 |
| [编译内置数值](../BuildConfig/README.md) | 通用参数集中入口、单位和重新编译方式 |
| [FORGE_PROGRESS.md](FORGE_PROGRESS.md) | 当前实现、本地检查结果和待验收项 |

## 设计方案

- [Nico工坊成就解锁设计方案](WORKSHOP_ACHIEVEMENT_DESIGN.md)：全开局名片、独立原生与伪人页面、9 项高难度挑战与 1 项原版通关成就，以及对应奖励和领奖规则。已接入源码，游戏内操作与保存重载待验收。
- [原生成就本地验证](probes/2026-10-02-native-workshop-implementation-validation.md)：检查结果、安装包哈希和游戏内待验收范围。

## 示例

- [MachineTemplates](../samples/Nicokobo.Forge.MachineTemplates/README.md)：自定义机器接入。
- [LogisticsExtension](../samples/Nicokobo.Forge.LogisticsExtension)：内容侧规则示例库，不作为插件安装。
- [WorkshopProbe](../samples/Nicokobo.Forge.WorkshopProbe/README.md)与[EffectRegistrationProbe](../samples/Nicokobo.Forge.EffectRegistrationProbe/README.md)：诊断示例，构建时需 `-IncludeProbes`。

## 历史证据

[历史进度](archive/FORGE_HISTORY.md)保留 2026-10-01 的版本、安装与验证记录；`probes/` 中的探针记录保留各自的原始范围。现行文档从当前源码说明契约，历史测试只支持当时明确验证的行为。

内容 Mod 的规则与验收在[关联仓库文档索引](../../probably-stolen/docs/README.md)维护。
