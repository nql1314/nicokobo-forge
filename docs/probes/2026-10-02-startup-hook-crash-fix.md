# Forge 启动补丁闪退修复

日期：2026-10-02，Asia/Shanghai。目标：Probably Stolen Demo，Steam Build `25382790`；Forge `0.6.5`。

## 原因与修复

玩家在 22:59 和 23:00 两次启动后退出。MelonLoader 日志停在 ModManager 初始化完成后，Windows 的 .NET Runtime 事件记录 `System.AccessViolationException: Could not prepare patch to target 0`，应用错误指向 MelonLoader `VERSION.dll`，异常代码 `0xc0000409`。

安装的 Forge 成就监听给 `GameInventory.UncheckedAccept(GameItem)` 和 `GameInventory.Expel(GameItem)` 安装 Harmony 补丁。当前原生构建中这两个方法为抽象声明，没有 RVA；互操作程序集把它们生成为可调用的虚方法包装，普通签名检查因此通过，原生 detour 仍会拿到零地址。

成就监听改为四种具体背包实现：`GameGridInventory`、`GameSlotInventory`、`GameGridScrollableInventory`、`GameCharacterRaidInventory`。放入、移出、数量和标签变化仍由原有成就回调观察。

`NativeHookSet` 在创建 Harmony 和安装任意补丁前，校验整组目标的原生入口。它通过 Il2CppInterop 的方法元数据查询及 Unity 版本适配器读取入口，拒绝抽象／开放泛型、缺少元数据及零执行地址；失败时只关闭对应功能。没有增加固定原生地址或修改游戏原始二进制。

## 离线验证

- 旧安装 DLL 的实际编译代码检出两个抽象原生补丁目标；修复后的实际编译代码对应 10 个具有非零 RVA 的监听入口。
- 新增独立补丁入口检查覆盖有效元数据、零执行地址、零元数据指针、普通托管方法、抽象方法、开放泛型，以及整组校验失败：8 项通过。使用当前 Il2CppInterop 的结构适配器和合成元数据，不启动游戏；已纳入 `Build-P0.ps1`。
- 框架 88 项互操作签名、原有领域检查、核心与六个示例通过；四个依赖 Mod 的领域检查、Release 构建及机核正式 Obfuscar 打包通过。构建日志中保留了 .NET 6 SDK 提示和独立检查工程的日志依赖版本警告，编译无错误。
- 与旧 DLL 比较的 1287 项公开类型／成员签名和程序集身份一致；四个内容包和独立 Forge 包使用同一新 Forge 哈希。

## 本地安装

只替换游戏 `Mods/Nicokobo.Forge-0.6.5.dll`。另外 14 个 Mod 和 `MelonPreferences.cfg` 的 SHA-256 不变，Forge 程序集仍只有一份。四个内容 Mod 的安装文件保持原样，公共 API 与程序集版本兼容。

| 文件 | SHA-256 |
| --- | --- |
| 原安装 Forge | `B83E485C802D9383970ECA18C26D50B61DB180C915DF89C0A8A3AE35B4E1DA0E` |
| 修复后 Forge | `CFAB22F4E9296768F88376BAB5A72F851A8F4D3827BEF8991BF2BA7315DF7433` |

证据和原 DLL 位于 `D:\workzone\probably-stolen\_build\startup-crash-fix-20261002`，包括 Windows 事件、启动日志、修改前源码、前后补丁目标报告、构建日志、兼容性比较和 [安装清单](../../../probably-stolen/_build/startup-crash-fix-20261002/installation.json)。

本轮没有启动游戏或写入玩家存档；实际进入主菜单、成就监听、工坊及保存重载仍待新进程验证。独立包已在本地更新，未发布网站。
