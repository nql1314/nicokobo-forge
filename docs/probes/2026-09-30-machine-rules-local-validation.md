# 2026-09-30 机器规则本地验证

目标：Probably Stolen Demo Build `25382790`，Forge `0.3.4`，合成扩展 `0.5.18`。本记录只对应本地源码、检查和发布包，不代表安装或运行验收。

## 改动

- 合成扩展配方去掉电耗字段，K01–K05、熔炉玻璃回收和伪人 K05 追加配方读取 `MachineryHelper.GetMachinePowerUsage`。机器沿用熔炉基础电耗 8，运行当前值由原生机器属性决定。
- 原版熔炉仅复用原生加工回调以实现性能额外次数；投入减少、产物增加、电池能量未异常增加才继续。保留模块装入/移出回调及游戏自己的纯度、助溶剂规则。零电耗成功批次可以继续，未产出或未消耗材料则停止。
- 扩展机器每次重新匹配配方、材料和电量；硅不再使用只识别原版矿石 ID 的 `SetPurityFromInput`。钛合金从本批金属锭继承纯度。
- 碎玻璃 `glass_shard`×2、酒瓶 `wine_bottle`×1各产石英×1；旧 SchemaVersion=1 的 17 条配方备份后升级为 2 的 19 条，保留已有材料/产物修改。

## 核对与验证

当前构建的静态原生调用链确认：`GetMachinePowerUsage` 读取 `machinery_drawn_power`；`ApplyModifiedPower` 从 `MACHINERY_DRAWN_POWER_BASE` 扣除整数百分比电耗修正；`SetPurityFromInput` 只识别原版矿石 ID。用户现有槽 21 保存文件的只读观察也显示熔炉和材料精炼机的基础电耗为 8；这份旧版存档不是本轮行为证据。

- Forge `scripts/Build-P0.ps1 -DefaultLogLevel INFO`：领域检查、核心、两个 P0 示例及物流示例 Release 构建通过。SDK 保留原有 net6.0 生命周期提示。
- 合成扩展领域检查：529 条断言通过，包含两条新配方、旧配置备份/保留/幂等升级、同机不同配方电耗一致及材料/电量不足。
- 伪人 Mod 领域检查：419 条通过；Release 编译零警告零错误。
- 合成扩展通过 `mods-melonloader/Build-MechcoreProtocol.ps1 -OnlyProject mechcore-protocol-synthesis-expansion` Release 编译与 Obfuscar 打包，零警告零错误。

本地发布目录：`D:\workzone\probably-stolen\_build\releases\synthesis-machine-20260930\mechcore-protocol-synthesis-expansion`，含配套 Forge、混淆 Mod DLL、README、change.log、默认配方及 Checklist。伪人 Mod 的对应 Release DLL 已另复制到同级 `aug-mechanical-ascension` 目录；三份 DLL 版本/哈希及检查结果见发布根目录的 `manifest.json`。包内 Forge 与最后构建产物哈希一致，默认配方为 SchemaVersion=2、19 条。

## 下一次实机核对

本轮未改动游戏 Mods、用户偏好或保存文件。安装对应候选后，用可丢弃档逐项复测性能 0/99/100/200 的 1/1/2/3 次加工、材料/电量不足停止、零电耗、碎玻璃和酒瓶回收、石英到硅、原版炼锭的质量/助溶剂结果，以及保存后退出重进读档。验收清单中的较早勾选不能替代本轮验证。
