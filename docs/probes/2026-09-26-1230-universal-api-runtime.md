# 12:30 重启：通用 API 加载与门控

> 命名迁移说明：本文记录发生在 Nicokobo Forge 命名迁移前。产品、程序集、API 与日志标签文字已统一为当前名称；其中散列值和运行结论仍属于当时构建，不能作为当前重命名产物的哈希或运行证据。

2026-09-26 12:30:10 完整重启，`MelonLoader/Latest.log` 显示三枚 Nicokobo Forge DLL 加载。已安装 `Nicokobo.Forge.dll` 的 SHA-256 为 `2E0108738AE01A4962057BCCD8946D659A1989BAC6296ABE9F46D113B9454DA0`。

## 已观察

- 目标 build `25382790` 的游戏和生成程序集哈希匹配；原有六个关键方法、`PlayerStore` 周目数据属性、库存直接子项属性与 `GameItem.IsTag` 签名均匹配。
- `[NicokoboForge/RunData] stageAllowed=True`、`[NicokoboForge/Inventory] readAllowed=True; transferAllowed=false`、`[NicokoboForge/NativeItem] directoryHook=installed`。
- 伪人插件向 Nicokobo Forge 提交 `nicokobo.aug.neural_interface_module` 节点，状态 `Accepted`；开局身份 `nicokobo.aug.mechanical_ascension.start` 认领状态 `Accepted`。两个 P0 示例声明最终均为 `DependenciesResolved`。

## 尚未证明

这次进程在主菜单附近结束；日志中尚无节点 `Applied`、普通物品工厂调用、库存 `CaptureDirect` 调用、`modData` 暂存/文件读回或物流搬运。因此这份记录只能证明加载、签名门控和声明提交，不能宣称物流玩法可用。

另有伪人插件 `AugHelper.OnGoodKill`、`OnBadKillWithSecGrace`、`CleanupKill` 三处探针安装失败，日志报 `TargetInvocationException`。进程退出时还出现 `Il2Cpp.AugHelper` 类型初始化的 `SEHException`，堆栈在 MelonLoader 卸载补丁路径。这属于独立的伪人/Interop 问题，未作为 Nicokobo Forge 物流 API 成功或失败的证据。
