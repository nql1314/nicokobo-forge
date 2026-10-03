# Nico 工坊点击穿透修复

日期：2026-10-03，Asia/Shanghai。候选 Forge `0.6.5`，目标 Demo Steam Build `25382790`。

## 原因与修改

旧实现只有 IMGUI 鼠标事件消耗和透明 Canvas。当前 `GameAssembly.dll` 的静态反汇编确认，`InputActionManager.Update`（RVA `0x7A9970`）独立分派鼠标按下、释放、移动和滚轮；`StoreUIManager.CheckRaycast`（`0x741730`）只检查原生画布及浮动窗口，随后写入 `hoveringStoreUI`，不会识别独立工坊画布。

- 工坊显示期间拦截上述两个原生入口：停止下层输入分派，并令店铺识别当前鼠标位于 UI 上。保留 EventSystem。
- `StoreUIManager.ResolveEscape`（`0x7451E0`）关闭并消费工坊 Escape，避免同时操作下层原生窗口。
- 三个必需钩子通过 `NativeHookSet` 整组预检、安装；任一失败即回滚并停用工坊。透明 UI 遮罩创建失败时关闭页面。
- 遮罩使用独立 Image 子节点，四边拉伸至全屏，明确包含 CanvasRenderer。IMGUI 同时消费滚轮事件。
- 所有关闭路径共享输入捕获状态。等左、右、中键全部松开，继续保护释放帧，再恢复下层输入；重复关闭不延长保护，重新打开不会继承待释放状态。保留原有选择、双击、标签和领取分派。

## 本地验证

执行 `scripts/Build-P0.ps1 -GameDir 'F:\SteamLibrary\steamapps\common\Probably Stolen Demo' -IncludeProbes`：

- 13 项新增输入状态断言通过，覆盖打开、空白区、关闭按下、持续按住、释放帧、重复关闭、关闭期间再次按下、重新打开及键盘／程序关闭。
- 95 项互操作签名检查、原有框架领域检查和 11 项独立补丁入口保护检查通过。
- Forge 核心和六个示例 Release 编译通过，包括 WorkshopProbe。独立检查工程仍报告 .NET 6 目标框架及 DiagnosticSource 依赖版本提示。
- 使用 Mono.Cecil 比较当前安装 Forge 与候选：程序集身份均为 `Nicokobo.Forge, Version=0.6.5.0`，1137 项公开类型／方法／字段签名一致。
- GameAssembly 与 Assembly-CSharp 哈希分别为 `3BEA17EEEC77ADAB6418918C28A8AA9A06F290582A2972639A48BD7E5A01AB44` 和 `9618787115686C0AF6DBC3B65BEE6DEA9DD33A8A998D01572CDC190A42657ACC`，与 Forge 的目标构建门控一致。

使用 `scripts/Pack-ModSite.ps1` 生成独立本地 Forge 包。候选 DLL SHA-256：`DEC6C5F6D5F48B8F114DBAF2D36C677C056C5935FAD5323A1ABB6D919A16E092`；ZIP 内 DLL 已读回核对。固定修复包、构建日志、修改前源码、兼容性回执及静态反汇编位于 [artifacts/workshop-input-2026-10-03](../../artifacts/workshop-input-2026-10-03/)。

## 验证边界

本轮没有替换游戏 DLL、启动游戏或执行原生测试。离线状态检查和静态反汇编不证明钩子已在新进程安装或游戏内点击已经隔离。待实际验收：节点／领取／空白背景不触发下层库存及店铺，关闭 X 的按住和释放不触发下层操作，Escape／N 关闭后正常输入恢复。
