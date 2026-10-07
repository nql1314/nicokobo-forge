# Nicokobo Compatibility Patches 0.1.5

中文名：**Nicokobo 兼容补丁合集**。用于统一维护 Probably Stolen 的第三方 Mod 兼容修复，后续补丁继续加入同一个 DLL。

独立文件：`Nicokobo.CompatibilityPatches-0.1.5.dll`。源码位于 Forge 仓库的 `compatibility/Nicokobo.CompatibilityPatches/`，每项补丁放在 `Patches/<补丁名>/` 并由 `Plugin` 统一接入。目标缺失或版本不符时只停用对应项；合集不引用或修改 Forge 核心，也不改写第三方 DLL。

当前目标游戏：Probably Stolen Demo **0.46D / Steam Build 25382790**、MelonLoader **0.7.3**。

## 已包含：WagePerksStartup

适用：Wage's Perks **1.3.4**。实现目录：`Patches/WagePerksStartup/`。

Wage's Perks 在 `OnInitializeMelon` 给原生类安装 Harmony 补丁。读取 IL2CPP 方法元数据会运行目标类的原生静态初始化，其中一些会通过 `LocHelper.Get` 同步读取本地化资源。2026-10-07 00:55 日志首先在 `ModuleEffectHelper` 报 `Attempting to use an invalid operation handle`；01:18 新进程确认 0.1.0 已拦住该项，但最早异常转为 `AugHelper`。0.1.0 的覆盖范围不完整。

本次检查 WagePerks 注册表目标中的 21 个原生静态初始化函数，使用当前 `GameAssembly.dll` 的指令与 Cpp2IL 地址核对，找到四个直接调用 `LocHelper` 文本入口的类：

| 原生类 | 延后的 WagePerks 挂载 |
| --- | --- |
| `ModuleEffectHelper` | `ModifyTempStatFromBaseByPercentage` |
| `AugHelper` | `CleanupKill` |
| `ItemFeatureList` | `BargainBuyingMarkup` |
| `StoreClientListTierSubstance` | `CreateDesperateAddict`、`CreateWornOutSpacer` |

核对范围是注册表目标的静态初始化及直接本地化调用，不代表排除了其他间接路径或其他 Mod 的早期访问。

本补丁以 MelonLoader priority **-10000** 在目标 Mod（priority 0）前初始化，只拦截其 `ManualPatcher.TryPatch` 中目标为上述四类的调用。其他补丁按原顺序执行。暂存所有原参数；在 `OnUpdate` 观察游戏已有的本地化初始化句柄，待句柄有效、完成且状态为 `Succeeded` 后，按原调用顺序重放五项挂载，各执行一次并核对原 Mod 的成功计数。

补丁不调用 `WaitForCompletion`，不主动启动本地化初始化。初始化失败、读取异常或挂载失败时取消待办，不反复重试；目标缺失、版本不符或初始化优先级不符时停用并记录原因。

## 已包含：WagePerksDestinyDice

适用：Wage's Perks **1.3.4**。实现目录：`Patches/WagePerksDestinyDice/`。

该版本开局通过 `CreateDestinyDice` 直接发放命运骰子（物品 ID `destiny_dice`），但 `Patches.PostfixInitDirectory` 没有调用现成的 `DestinyDice.RegisterToDirectory`，DLL 内也没有其他调用。物品可在新开局出现，读档时却没有重建工厂；配套 Forge 的缺失 ID 清理也会将其判为缺失物品。

合集在 WagePerks 已有的托管目录回调完成后补上原注册调用，沿用它的容器、设施和 Mod 目录初始化时机。原函数持有 IL2CPP 工厂委托，并跳过目录内已有的同 ID；补丁检查注册后该 ID 确实存在，失败时停用此项。原生存档解码继续恢复原有物品身份、位置和骰子累计值、触发次数等标签；不重新发放骰子或重置已保存的进度。

此修复只对存档中仍有骰子节点的记录有效。已经在骰子消失后再次保存的记录，需要从此前仍包含骰子的存档或备份恢复。

## 修复归属调整

合集 0.1.2 / 0.1.3 曾临时提供 `AugOpeningReputation`。该问题属于伪人自身的开局实现：使用受原生倍率影响的声望接口，却要求精确应用配置差值。修复现已迁入伪人 **0.1.52**，直接调用 `ModReputationRaw`，保留其他特性造成的基础声望、周目门控、结果校验及保存回读。

合集 0.1.3 曾临时提供妙妙箱白名单 `AugOpeningInventory`。slot 97 已成功完成初始化，但 slot 98 增选捡漏直觉等 Perk 后，背包又出现 ID 为 0 的 `void_bead_storage`，仍在脑机接口身份读取处进入 `needsRecovery`。接口本身为 ID 80、自有、单件、价值 100、1×1；失败原因是校验范围覆盖了其他 Mod 的赠品。

伪人 **0.1.52** 已将读取范围改为自己的脑机接口，保留接口正 ID、归属、数量、价值、尺寸及与其他物品的 ID 冲突校验。其他赠品的无效或重复编号不影响接口证明，也不需要逐项白名单。合集 **0.1.4** 已删除上述两项及其动态挂载；后续版本继续由伪人自身处理开局校验。更新时必须同时更新伪人和合集，避免旧的妙妙箱白名单覆盖新读取逻辑；已失败的旧开局不会自动续发。

## 安装与移除

1. 关闭游戏。
2. 安装伪人时须使用 0.1.52 或更新版本，保留原 `WagePerks.dll`，移出旧合集（及旧的 `Nicokobo.Compatibility.WagePerksStartup` 单项 DLL），放入合集 0.1.5；每个 Mod DLL 只保留一份。
3. 重新启动。日志应先出现“已拦截”，再出现“已暂存”，本地化完成后出现五项“延后挂载成功”。物品目录初始化后应出现“命运骰子目录注册已确认”。声望精确调整与脑机接口校验由伪人自身执行。
4. 移出兼容补丁 DLL 即可停用。

此补丁无法修复当前进程中已经失败的静态初始化，必须使用新进程。合成扩展 schema 8 配方警告属于另一问题，本补丁不改动配置或存档。

## 构建与打包

在此目录执行：

```powershell
.\Build-Package.ps1 -GameDir 'F:\SteamLibrary\steamapps\common\Probably Stolen Demo'
```

脚本只检查、编译并生成 `dist/compatibility/nicokobo-compatibility-patches/` 内的统一 DLL 与此说明，不安装到游戏。针对性检查覆盖等待、完成后只执行一次、失败停用、重放期间放行，以及已安装 WagePerks 的入口签名、目录注册回调、骰子工厂委托、原生目录 Has、加载优先级与合集标识，并确认已经删除的声望与背包补丁不再编入 DLL。

检查需要目标第三方 Mod 确实安装在 `<游戏目录>\Mods\WagePerks.dll`；该文件缺失时脚本会在检查阶段直接报错，这是本步骤的环境前提，因此不把它挂进通用的 Forge／内容 Mod 构建入口。

该产物随后由 Forge 的 `scripts/Pack-ModSite.ps1` 复制进独立 Forge 玩家包，并在发布说明中标为可选：Forge 不会自动安装或加载它，玩家仅在安装了对应第三方 Mod 时手动复制到 `Mods/`。想要刷新 Forge 包内的副本，先在此目录打包，再重跑 Forge 打包入口。

伪人领域检查覆盖声望基线、两种零 ID 赠品组合、未知赠品与彼此重复的编号，以及接口身份冲突、归属、数量、64 位价值和尺寸。

此前验证：0.1.1 的五项延后挂载、0.1.2 的声望初始化及 0.1.3 在 slot 97 的完整初始化已由用户新进程日志与存档确认。0.1.4 完成 Release 编译（0 警告、0 错误）及 37 项离线检查；伪人 0.1.52 的 Release 编译、646 项领域检查及 11 项售价特性复用检查通过。旧运行证据不自动覆盖后续候选。

当前 0.1.5 新增命运骰子注册修复，Release 编译为 0 警告／0 错误，45 项针对性离线检查通过。2026-10-07 只读回查 slot 84，主库存仍包含骰子 uniqueId 74、数量 1 及累计值、触发次数、门槛等原标签。本轮未打包、安装或执行原生测试；发布目录与游戏仍为此前的 0.1.3。仍需新进程读取包含骰子的存档，核对状态和保存后重载。
