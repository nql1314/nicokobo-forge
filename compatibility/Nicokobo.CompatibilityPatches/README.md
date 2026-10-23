# Nicokobo Compatibility Patches 0.1.7

中文名：**Nicokobo 兼容补丁合集**。用于统一维护 Probably Stolen 的第三方 Mod 兼容修复，后续补丁继续加入同一个 DLL。

独立文件：`Nicokobo.CompatibilityPatches-0.1.7.dll`。源码位于 Forge 仓库的 `compatibility/Nicokobo.CompatibilityPatches/`，每项补丁放在 `Patches/<补丁名>/` 并由 `Plugin` 统一接入。目标缺失或版本不符时只停用对应项；合集不引用或修改 Forge 核心，也不改写第三方 DLL。当前变更见 [change.log](change.log)；0.1.7 整理本次修复的发布标识，后文原生证据保留实际受测的 0.1.6 版本。

当前核对游戏：Probably Stolen Demo **0.46D / Steam Build 26017150**、MelonLoader **0.7.3**。

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

## 已包含：PCExpansionScalars

适用：本地归档中的深空装机 **2.6**。实现目录：`Patches/PCExpansionScalars/`。

旧快照可能保存 `isDebugMenu`，当前游戏的同名属性只读，可写入口为 `_isDebugMenu_k__BackingField`。装机原 `ApplyScalars` 直接按旧名写入会失败；恢复后的新字段名也会让 `Equivalent` 和维修 `VerifyPreserved` 判定不一致。

补丁在恢复前把普通属性名、`_名称_k__BackingField` 和 `<名称>k__BackingField` 映射到当前可写标量属性；比较时仅规范脱离原记录的 DTO 副本中的字段别名，包括嵌套库存、特性、状态和效果。原始恢复方法、完整快照比较、维修字段和标签校验继续执行。原记录、原生 Payload、数量、身份和标记 ID 不被改写；未知字段或别名值冲突仍阻止恢复，不会被静默删除。此项不跳过回读校验，不能据此认为任意旧版存档格式都可迁移。

不要与第三方 `PCExpansionLegacyFix` 同时使用：它会直接跳过完整回读校验，并可能阻止或移除 Forge 的数量观察钩子。检测到该旧包时，本项停用并提示；合集不会自动卸载别人的补丁。

## 本地对应 Mod 的核对结果

- `mods/内容扩展/` 中酿酒扩展 **3.6.2** 的充电引用均为当前 `ChargeBattery(GameItem, int, bool)`，未发现报告中已失效的双参数入口。本次不覆盖其充电算法，也不吞掉浓缩机异常；旧双参数 DLL 不在本次新增修复范围内。
- 岁物品包 **0.7** 没有旧兼容包针对的 `ShuNianBubble.PlaceIntoFurnace`；当前额外锭走 `GraphUtils.TryAcceptAllOrDestroy`。本次不照搬强制改投玩家的旧补丁，未对当前熔炉路径作原生安全性结论。
- Forge 数量桥接与 `SetUnitCount`／`SetAmount` 共享原生入口由 Forge 本身修复。合集不增加数量护栏、递归截断探针或自动摘钩。

## 修复归属调整

合集 0.1.2 / 0.1.3 曾临时提供 `AugOpeningReputation`。该问题属于伪人自身的开局实现：使用受原生倍率影响的声望接口，却要求精确应用配置差值。修复现已迁入伪人 **0.1.52**，直接调用 `ModReputationRaw`，保留其他特性造成的基础声望、周目门控、结果校验及保存回读。

合集 0.1.3 曾临时提供妙妙箱白名单 `AugOpeningInventory`。slot 97 已成功完成初始化，但 slot 98 增选捡漏直觉等 Perk 后，背包又出现 ID 为 0 的 `void_bead_storage`，仍在脑机接口身份读取处进入 `needsRecovery`。接口本身为 ID 80、自有、单件、价值 100、1×1；失败原因是校验范围覆盖了其他 Mod 的赠品。

伪人 **0.1.52** 已将读取范围改为自己的脑机接口，保留接口正 ID、归属、数量、价值、尺寸及与其他物品的 ID 冲突校验。其他赠品的无效或重复编号不影响接口证明，也不需要逐项白名单。合集 **0.1.4** 已删除上述两项及其动态挂载；后续版本继续由伪人自身处理开局校验。更新时必须同时更新伪人和合集，避免旧的妙妙箱白名单覆盖新读取逻辑；已失败的旧开局不会自动续发。

## 安装与移除

1. 关闭游戏。
2. 安装伪人时须使用 0.1.52 或更新版本，保留目标第三方 Mod，移出旧合集（及旧的 `Nicokobo.Compatibility.WagePerksStartup` 单项 DLL），放入合集 0.1.7；每个 Mod DLL 只保留一份。启用装机字段兼容时，还须移出旧 `PCExpansionLegacyFix`。
3. 重新启动。匹配 Wage's Perks 1.3.4 时，日志应先出现“已拦截”，再出现“已暂存”，本地化完成后出现五项“延后挂载成功”；物品目录初始化后应出现“命运骰子目录注册已确认”。匹配装机 2.6 时应出现“已安装储物／维修字段别名兼容”。声望精确调整与脑机接口校验由伪人自身执行。
4. 移出兼容补丁 DLL 即可停用。

此补丁无法修复当前进程中已经失败的静态初始化，必须使用新进程。合成扩展 schema 8 配方警告属于另一问题，本补丁不改动配置或存档。

## 构建与打包

在此目录执行：

```powershell
.\Build-Package.ps1 -GameDir 'F:\SteamLibrary\steamapps\common\Probably Stolen Demo'
```

脚本只检查、编译并生成 `dist/compatibility/nicokobo-compatibility-patches/` 内的统一 DLL、此说明与 `change.log`，不安装到游戏。针对性检查覆盖等待、完成后只执行一次、失败停用、重放期间放行，以及已安装 WagePerks 的入口签名、目录注册回调、骰子工厂委托、原生目录 Has、加载优先级与合集标识，并确认已经删除的声望与背包补丁不再编入 DLL。

离线检查根据实际可用目标执行对应契约组；当前没有 Wage's Perks 1.3.4 时明确跳过其目标专属契约组，仍运行通用逻辑、合集标识和新字段兼容检查。可向 `Checks` 的第三个命令行参数传入解包目录（其中包含 `PCExpansion.dll`、`BrewingExpansion.dll` 和 `PSItemPack_Shu.dll`），检查真实归档目标；它只调用装机的托管 DTO 校验方法，不启动游戏或调用原生业务。

该产物随后由 Forge 的 `scripts/Pack-ModSite.ps1` 复制进独立 Forge 玩家包，并在发布说明中标为可选：Forge 不会自动安装或加载它，玩家仅在安装了对应第三方 Mod 时手动复制到 `Mods/`。想要刷新 Forge 包内的副本，先在此目录打包，再重跑 Forge 打包入口。

伪人领域检查覆盖声望基线、两种零 ID 赠品组合、未知赠品与彼此重复的编号，以及接口身份冲突、归属、数量、64 位价值和尺寸。

此前验证：0.1.1 的五项延后挂载、0.1.2 的声望初始化及 0.1.3 在 slot 97 的完整初始化已由用户新进程日志与存档确认。0.1.4 完成 Release 编译（0 警告、0 错误）及 37 项离线检查；伪人 0.1.52 的 Release 编译、646 项领域检查及 11 项售价特性复用检查通过。旧运行证据不自动覆盖后续候选。

当前 0.1.5 新增命运骰子注册修复，Release 编译为 0 警告／0 错误，45 项针对性离线检查通过。2026-10-07 只读回查 slot 84，主库存仍包含骰子 uniqueId 74、数量 1 及累计值、触发次数、门槛等原标签。本轮未打包、安装或执行原生测试；发布目录与游戏仍为此前的 0.1.3。仍需新进程读取包含骰子的存档，核对状态和保存后重载。

2026-10-22：0.1.6 候选 Release 编译通过，0 警告、0 错误；74 项离线检查通过。除别名恢复、原记录不变、64 位值、未知字段、冲突别名、嵌套库存和标签／特性保留外，还使用游戏所带 HarmonyX 把实际比较前缀挂到装机原始 `Equivalent`／`VerifyPreserved`，验证参数确实替换为 DTO 副本，数量和原生 Payload 不一致仍被拒绝。比较前缀使用 `ref object __0 / __1`；仅替换 `__args` 数组元素在当前 HarmonyX 中不会传入原方法。本机没有 Wage's Perks 1.3.4，其目标专属契约组已明确跳过。

同日，在游戏 0.46D / Build 26017150 的隔离窗口化进程中，保留现有 Mod 并加入装机 2.6 与合集候选，共 27 个 Mod；今天改动的 87 项原生检查通过。装机覆盖旧 `isDebugMenu` 字段快照恢复、完整原始回读校验、未知字段拒绝及候选清理，并执行空机箱和主板的 `RepairBody`，核对修后实际型号、数量、价值、身份和保留校验。同步验证 Forge 数量入口与实际到店进货、机器人原生点击回调、管理器配置归属和真实 GUI 绘制。证据位于本机 `D:/workzone/probably-stolen/_build/native-test/20261022-changes-e8338cd1/native-09-windowed/`。完整工坊维修扣料流程、真实旧玩家档的 ES3 保存重载未在此轮覆盖；未将候选安装到正式游戏。
