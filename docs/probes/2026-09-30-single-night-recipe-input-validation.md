# 2026-09-30 每晚单批与配方输入验证

目标：Probably Stolen Demo Build `25382790`，Forge `0.3.5`，合成扩展 `0.5.19`。本记录对应本地候选；安装、运行和重载需分别验证。

## 改动

- 删除性能加批循环与原版熔炉重复调用。固体每晚执行一个有效配方批次；K04 每晚只处理一件肉或其已有进度，即使完成也不接着处理下一件。
- `ResolveBatchCount` 保留公共成员签名以兼容旧内容 DLL，运行时不再读取；合成扩展及伪人 K05 提供者去掉次数回调。每批产量、当前电耗、效率、质量/纯度及 K04 耗水规则保留。
- 熔炉投料前置判定按注册配方索引放行输入，包括酒瓶、碎玻璃、空啤酒瓶及玻璃刀；配置修改投入 ID 后重启也会更新索引。其他原版物品仍由原生规则接纳。
- 删除废弃的原生批次重复检测 helper 和对应检查；更新手册、机器说明及当前验收清单。旧版本日志和探针记录保留为历史证据。

## 本地检查

- 当前 IL2CPP 包装签名：`MachineFurnace.__c__DisplayClass0_0._Furnace_b__1(GameItem, GameInventory):bool`；投料补丁和原生加工补丁沿用当前构建的目标。
- Forge 领域检查通过，包含四条玻璃原料、配置投入 ID、无关物品拒绝及注册列表冻结；核心、两个 P0 示例和物流示例 Release 构建通过。领域工程保留原有 net6.0 生命周期提示。
- 合成扩展 522 条领域断言通过，包含旧 SchemaVersion=1 配置备份/保留/升级到 19 条配方及原料数量规则。
- 伪人 Mod 419 条领域断言通过。
- 合成扩展 Release 编译与 Obfuscar 发布通过，伪人 Mod Release 编译通过，均零警告零错误；两仓库 `git diff --check` 通过。
- 对生成的 DLL 做元数据核对：Forge 0.3.5、合成扩展 0.5.19、伪人 Mod 0.1.5；处理代码均未读取/设置 `ResolveBatchCount`。Forge 的加工分派只各有一次 `Execute`/`ProcessLiquid` 调用，已无熔炉重复调用；保留旧属性以兼容已有内容 DLL。record 自动生成的 `PrintMembers` 仍可读取该属性以格式化文本，与加工调度无关。

## 发布产物

规范打包入口：`mods-melonloader/Build-MechcoreProtocol.ps1 -OnlyProject mechcore-protocol-synthesis-expansion -DefaultLogLevel INFO`，传入最后构建的 Forge DLL。发布目录为 `D:\workzone\probably-stolen\mods-melonloader\dist\mechcore-protocol-synthesis-expansion`；六项文件已压缩为 `D:\workzone\probably-stolen\_build\releases\synthesis-single-night-20260930\synthesis-forge-0.5.19.zip`，ZIP 内 6/6 哈希与发布目录一致，默认配方为 SchemaVersion=2、19 条。检查结果、逐项哈希及同轮伪人 Release DLL 位于同级 `manifest.json` 和 `AugMechanicalAscension.dll`。

- Forge SHA-256：`06F1392D481A29C4970D25973D0646D7CF1120184996D497F931E0A1C1EF0037`。
- 合成扩展混淆 DLL SHA-256：`3FAC1C100454AD1B9C47A881C276E26706147CA63E15019E13A27F5B8C641616`。

## 运行边界

本轮读取的游戏进程仍加载合成扩展 0.5.17 和 17 条配方；碎玻璃与酒瓶在该安装版中尚无配方声明。游戏运行期间不替换 DLL，不修改用户偏好或保存文件。

新候选安装后需验证四种玻璃原料的单拖/组拖、修改投入 ID 后重启、性能 0/99/100/200 下多批材料仅扣一批、K04 完成后下一件留到次夜，以及保存后退出重进的一致性。检查步骤见合成扩展当前 Checklist。
