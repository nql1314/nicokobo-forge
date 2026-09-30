# 2026-10-01 Forge 功能边界与机核协议重构：本地验证

状态：源码、领域检查、当前互操作元数据签名、Release 编译与本地发布包已验证。本轮未复制 DLL 到游戏 `Mods/`、未启动游戏、未修改玩家存档、未发布线上。Native Hook 安装、原版 UI、拖放、过夜行为和新档重载均未验证。

## 目标与改动

目标为 Steam Build `25382790`，实读 `appmanifest_4349200.acf` 的 `buildid` 和 `TargetBuildID` 均一致。当前游戏文件实读 SHA256：

- `GameAssembly.dll`：`3BEA17EEEC77ADAB6418918C28A8AA9A06F290582A2972639A48BD7E5A01AB44`
- `Assembly-CSharp.dll`：`9618787115686C0AF6DBC3B65BEE6DEA9DD33A8A998D01572CDC190A42657ACC`

以已完成的 0.4.0 机器模板为基础，Forge 0.5.0 删除旧聚合 API，按功能划分注册、运行时、生命周期、模组、文本、库存、周目、电量和液体入口。目录与生命周期接入共用，补货从注册实现中独立。模组矩阵和合成扩展将重复接入改为公共订阅；物流和其他调用者同步入口与版本。旧档迁移分支删除，常规诊断减少到按需／DEBUG。实现边界见 [API_BOUNDARIES.md](../API_BOUNDARIES.md)。

内容方自行维护的常规 Harmony 目标静态计数：模组矩阵 17 → 4；合成扩展 13 → 10。这不是总游戏目标数或运行冲突率的实测，公共适配器仍安装必要接入。

## 验证

执行入口：

```powershell
.\mods-melonloader\Build-ForgeMods.ps1 -DistRoot 'D:\workzone\probably-stolen\_build\releases\forge-api-boundaries-20261001'
# 清理一处可空性编译警告后，按仓库规范重新生成机核发布 DLL。
.\mods-melonloader\Build-MechcoreProtocol.ps1 `
  -GameDir 'F:\SteamLibrary\steamapps\common\Probably Stolen Demo' `
  -ForgeDll 'D:\workzone\nicokobo-forge\src\Nicokobo.Forge\bin\Release\Nicokobo.Forge.dll' `
  -DistRoot 'D:\workzone\probably-stolen\_build\releases\forge-api-boundaries-20261001'
# 在 Forge 仓库打包网站用本地 ZIP。
.\scripts\Pack-ModSite.ps1
```

统一构建退出 0；最终机核编译与规范 Obfuscar 打包退出 0。核心、六个示例和四个调用工程均通过 Release 编译；最终 Mod 发布编译 0 错误、0 警告。领域控制台工程继续产生既有 NETSDK1138（游戏使用的 net6.0 目标）提示。

| 项目 | 通过项 |
| --- | --- |
| 互操作程序集元数据签名 | 32；Cecil 只读，不初始化或调用游戏类 |
| Forge 机器模板目录 | 39 条断言 |
| Forge 批次计算与事务故障注入 | 119 条断言 |
| Forge 本轮运行边界 | 88 条断言：拥有者隔离、稳定分派、准入批量提交、扣电和补偿故障 |
| Forge 其他既有注册／物流领域检查 | 通过 |
| 机械飞升 | 422 条 |
| 模组矩阵 | 89 条 |
| 物流脉络 | 21 + 37 + 27 条 |
| 合成扩展 | 603 条 |

所有活动 C#／构建脚本调用者已查找，未发现旧 API 引用；两个工作区 `git -c core.safecrlf=false diff --check` 通过。历史探针记录保留当时版本与 API 名称。

## 产物与读回

机核 DLL 经 `Build-MechcoreProtocol.ps1` 的 Release／Obfuscar 路径生成，私有中间文件和映射保留在 `_build/mechcore-obfuscation/20261001-060200-a8be9b05/`，不作为发布文件。机械飞升使用其普通 Release 候选 DLL。

| 产物 | 版本 | SHA256 |
| --- | --- | --- |
| Nicokobo.Forge | 0.5.0 | `2CB5B7ADE94F290A519B6F7454EED0CA49A3562DA7A0788F8AFBCAC4FC8100C0` |
| MechcoreProtocol.ModuleMatrix | 1.1.0 | `250C52ACE1294E36C4DCA101CAC4C08A8AF133AB9141278F807A890230D71FE0` |
| MechcoreProtocol.LogisticsNexus | 0.2.0 | `102DEA685BC112892885D6B9CD9CC9586BB2DA249EF758BECE020653D9D5055B` |
| MechcoreProtocol.SynthesisExpansion | 0.8.0 | `DCF49E37520709D49ADDCD196190CB915EB9B5697A302A27F88A7CFB3FDC2F74` |
| AugMechanicalAscension | 0.1.8 | `5E07EA6798A458F8E92AC448B5C31D86F27A27B303C3D2806326A3D5AA8C50CD` |

四个配套目录共八个 DLL 独立读回：四份 Forge 哈希均与核心一致，四个 Mod 的程序集引用均为 `Nicokobo.Forge 0.5.0.0`。本地清单在 `D:\workzone\probably-stolen\_build\releases\forge-api-boundaries-20261001\validation.json`，`installed` 与各项实机 `Verified` 字段均为 false。

框架 ZIP 为 `D:\workzone\nicokobo-forge\dist\Nicokobo.Forge-0.5.0.zip`。独立读取 ZIP 确认仅含 `Nicokobo.Forge-0.5.0.dll` 与 `README.md`，压缩包内 DLL 哈希与核心相同。这些证据不代表游戏加载、原生交易或存档成功。
