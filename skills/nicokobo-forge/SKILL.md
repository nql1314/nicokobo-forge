---
name: nicokobo-forge
description: 在 Nicokobo Forge 仓库开发、审查、接入或验证公共 API、原生注册、网络升级、工坊和周目数据时使用；不用于一般 Probably Stolen 内容 Mod 的玩法设计。
---

# Nicokobo Forge 开发与接入

Forge 是 Probably Stolen Demo 的公共前置 API。先确认任务是修改框架、让内容 Mod 调用框架，还是验证已安装产物；只在相应项目内改动。内容规则、物品数据和资源交易由内容 Mod 持有，通用注册、门控和适配由 Forge 持有。

## 从当前状态开始

1. 定位包含 `src/Nicokobo.Forge/Nicokobo.Forge.csproj` 的仓库，检查工作区改动和目标游戏构建。先读 `README.md`、`docs/FORGE_PROGRESS.md`、`docs/SCOPE.md`；涉及旧运行结论时再读对应的 `docs/probes/` 记录。历史日志与哈希只适用于记录中的安装产物。
2. 从 `ForgeCapabilities.Current`、相关 API 的 `Snapshot()`、`Plugin.cs` 和 `Diagnostics/BuildProbe.cs` 确认能力门控与生命周期。`Accepted` 表示声明进入注册中心，`Applied` 才表示该项被观察到进入原生目录；门控为 true 仍不证明具体内容或存档成功。
3. 读取任务触及的 API、注册模型、内容侧调用者和测试。保持 owner、稳定 ID、委托持有与内容所有权一致；不要把单个内容 Mod 的玩法逻辑挪进公共框架。

## 按功能核对边界

| 任务 | 优先核对 |
| --- | --- |
| 物品、节点、设施、模块或效果 | `ForgeNativeApi`、`ForgeNativeEffectApi`、对应注册目录与 `Snapshot()`；目录初始化、原生 ID 冲突、工厂产物类型和效果随机池资格分开验证。 |
| 网络升级 | `ForgeNetworkUpgradeApi` 与 `NetworkUpgradeCatalog`；当前原生对象、副标题、解锁动作及 UI ID 都要做所有权检查。额外资源由内容回调自补偿；购买失败核对同一周目/槽位，补偿保存后读回文件，再用可丢弃档验证重载。 |
| Nico 工坊 | `ForgeWorkshopApi` 和内容侧标签页；`Snapshot()` 可返回空值或变化数据，使用已校验快照；Forge 只管展示与事件分派，解锁回调负责重新验资、扣费、效果与保存。 |
| 开局、周目或库存 | `ForgeStartApi` 只认领开局身份；`ForgeRunDataApi.Stage` 只暂存内存；`ForgeNativeInventoryApi` 目前只读和预检，`InventoryTransfer` 未开放。实际保存、载入和物品移动要独立验收。 |

必需 Harmony 钩子只要有一项失败，就撤销本功能已装回调或关闭统一门控；不要留下半启用能力。按游戏对象就绪事件接入 `PlayerStore`、目录和 UI，避免在早期初始化或逐帧循环中强取单例。遇原生同 ID 对象、动作或 UI 冲突时保留其他扩展的结果，并让 Forge 对应能力显式停用。

## 验证和交付

- 用当前游戏目录执行 `scripts/Build-P0.ps1 -GameDir <游戏目录>`；它覆盖领域检查、核心、两个 P0 示例及物流示例，**不包含**工坊和效果探针。改动后按需单独构建相应样例。直接构建核心工程时也要传 `-p:GameDir=<游戏目录>`。
- `scripts/Pack-P0.ps1` 只打包核心与两个 P0 示例。打包、安装与启动是不同步骤；核对目标游戏构建、清单、实际 `Mods/` 文件哈希和新进程日志后，才能把运行结论归于当前代码。
- 给 Mod 网站准备文件时，先完成 Release 构建，再运行 `scripts/Pack-ModSite.ps1`。它将面向玩家的双语 `README.md`、`release.md`、唯一的核心 DLL，以及项目已有的 `cover.png` 放入 `dist/nicokobo-forge/`；下载 ZIP 只含 DLL 与两份文案。不要把 P0 诊断示例或探针加入玩家安装包。
- 对每项结果分别报告纯逻辑检查、编译、补丁安装、目录应用、游戏行为、保存文件回读与重新载入。原生交易或存档的成功不能只凭内存状态或单次 `SaveGame()` 返回值断言；缺少可丢弃测试档时明确标为待实机验证。
- 更新进度时优先维护 `docs/FORGE_PROGRESS.md` 的当前状态和证据；保留 `docs/P0_PROGRESS.md` 及旧探针记录的日期与产物边界。
