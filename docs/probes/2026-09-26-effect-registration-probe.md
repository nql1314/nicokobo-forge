# Nicokobo Forge 效果注册测试：2026-09-26 13:43

> 命名迁移说明：本文记录发生在 Nicokobo Forge 命名迁移前。产品、程序集、API 与日志标签文字已统一为当前名称；其中散列值和运行结论仍属于当时构建，不能作为当前重命名产物的哈希或运行证据。

测试插件：`samples/Nicokobo.Forge.EffectRegistrationProbe`。游戏 `GameAssembly.dll`
SHA-256 `3BEA17EEEC77ADAB6418918C28A8AA9A06F290582A2972639A48BD7E5A01AB44`；
已安装 `Nicokobo.Forge.dll` SHA-256
`E64227B0E99DB95E0F3BC6039F27F6E77D456B9C043F8FBDA8D72AA6B52F2F06`；
测试 DLL 源与安装文件 SHA-256 均为
`BE7BE9EBF7C3FF48E20C14CDACA2FB4D843267346FB7EF4A9377E8F2D0235B9A`。
工程编译为零警告、零错误。

本次 `MelonLoader/Latest.log` 的关键顺序：

- 13:43:41.977：`RegisterEffect` 返回 `Accepted`，声明不进随机池。
- 13:43:42.002：Nicokobo Forge 安装效果目录钩子，随机池钩子待目录就绪。
- 13:43:54.361：目录就绪后安装随机池钩子，`registryReady=true`。
- 13:43:54.366–.368：测试工厂被调用，效果 ID 达到 `Applied`。
- 13:43:54.370：目录再次触发时报告 `AlreadyApplied`，没有覆盖原条目。
- 13:43:55.323：测试插件从原生效果表读到同一 ID，`nativeRegistryPresent=True`，能力门控为 `True`，`callbackCount=0`。

截至 13:44:20，游戏仍在运行；本次日志未见 `TargetInvocationException`、
`TypeInitializationException`、`SEHException`、`status=Failed` 或 `status=Conflict`。
同次启动中伪人开局卡 `enabled=True`，其节点、义眼和手写单目录注册均达到
`Applied`。这些证据证明 Nicokobo Forge 的**延迟效果注册链路**可在当前构建下把测试条目
放入原生效果表；没有证明 `onAny` 回调、随机池排除、实际数值或保存重载。
