# 2026-10-01 生产计价本地验证

目标为 Probably Stolen Demo Steam Build `25382790`。本次执行源码检查、离线元数据检查、领域断言、Release 编译、Obfuscar 和本地打包；没有启动游戏、安装 DLL 或执行原生测试。

## 实现边界

Forge 0.6.1 提供本批材料内在价值、原生类别投入、固体品质前基础价值标签及液体各组分的实际／品质前价值。具体配方、默认价、机器增值率和品质去重由内容 Mod 声明。游戏原生 `AccumulateFeatureStages`、`ComposeStagedValue` 和后代物品列表负责内在状态计价；制造排除市场与事件修正。原生子物品每个计一次，本批材料件数只乘被选中的物品。

Synthesis 0.9.5 采用 K01／K02／K03／K04／K05 增值 75%／50%／25%／50%／25%。默认工厂价独立保留，不作为实际制造下限。输出带品质时保留原料上游加工基础价值，去掉原料纯度／品级溢价，最后只加产物品质；K05 无输出品级，保留材料实际内在价值。

Aug 0.1.10 提交六件义体与六条 K05 追加配方，移除身份模块与最终前置。三条含液配方仅消耗指定蛋白液 1000／1000／500 ml；合成声带接纳有曲目内容的音乐磁带类别。提示纸条的新图标尚未完成时，候选包保留原生手札图标；资源存在时按原嵌入入口加载，不生成替代图像。

## 通过的检查

- Forge 离线元数据签名 49 项；Synthesis 熔炉签名 2 项。元数据检查未初始化或调用原生游戏类。
- Forge 机器目录 45、批次及失败恢复 119、生产价值与液体账本 25、运行边界 88 项断言通过。
- Aug 433、模组矩阵 89、物流 L0=21／resource=37／power=27、Synthesis 755 项领域断言通过。
- `mods-melonloader/Build-ForgeMods.ps1 -DefaultLogLevel INFO` 完成 Forge 和六个示例、四个内容调用者的 Release 编译。三个机核项目通过仓库要求的 `Build-MechcoreProtocol.ps1`／Obfuscar；四个包使用同一 Forge DLL。
- 默认 JSON 为 schema 6，共 20 条，与源码默认文件的 SHA256 相同；K01–K05 共 22 条，连同原版熔炉四条共 26 条。
- 发布 DLL 的程序集版本及 Forge 0.6.1 引用通过离线读取；Synthesis／Aug 均无身份模块嵌入资源。两个仓库的 `git diff --check` 通过。

领域检查项目有 SDK 的 net6.0 生命周期提示；正式 Mod Release 编译均为零警告、零错误。

## 本地包哈希

| 文件 | SHA256 |
| --- | --- |
| Nicokobo.Forge-0.6.1.dll | `51AF7F16CE8FFFE3A0A5A1201A39121E0D1CABB968C66D72006A52925F8CB173` |
| MechcoreProtocol.SynthesisExpansion-0.9.5.dll | `9BD98702D72EDE466B25B11FE217983F83458E189415A3A6F3711A6C2D39E0A9` |
| AugMechanicalAscension-0.1.10.dll | `C425A936A23A3F09E1ECC3681C69C72D49D8FECD34579313844F6943141B02D9` |
| recipes.default.json | `DB04B91BC1A0E8CBD91F970D4C1F1C4E4A83D29B05513D4FF90A0EB14087DF97` |

本地打包日志对应 `_build/mechcore-obfuscation/20261001-202117-e974a517`。随后提示纸条资源落盘，Aug 单独重新编译并更新包内 DLL；离线元数据确认该资源已嵌入，身份资源为零。生产计价文档、配方表、伪人计划和未勾选的验收清单同时随包提供。

上述结果不证明实际过夜、原生倾倒、价格成交或保存重载。新原生价值调用与液体 Hooks、品质标签以及读档后价格保持须按当前候选的实机 Checklist 单独验收。已有 0.6.0 的原生探针记录不覆盖本次计价修改。
