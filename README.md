# Nicokobo Forge / 模组前置框架

Author: Nicokobo · Version: 0.3.9

## Summary / 摘要

**English:** A shared dependency for Probably Stolen Demo content mods, supporting new items, effects, and the Nico Workshop. Forge adds no gameplay content by itself.

**中文：**Probably Stolen Demo 内容 Mod 的共用前置，支持新物品、效果与 Nico 工坊；单独安装不会增加玩法内容。

This local `0.3.9` candidate targets **Probably Stolen Demo Steam Build 25382790**. Other game builds are unverified; Forge disables native features when the game build or required signatures do not match. Each registered machine processes at most one batch per night. Expanded recipe admission and processing still require gameplay checks.

当前 `0.3.9` 面向 **Probably Stolen Demo Steam Build 25382790**。其他游戏构建尚未验证；构建或必要签名不匹配时，Forge 会停用对应原生功能。每台注册机器每晚只处理一批；原版熔炉不再额外重复加工，原版纯度、助溶剂及模块装入/移出回调仍由游戏处理。输入规则按注册配方建立索引，熔炉可接纳新增原料；加工读取机器当前电耗。原版熔炉可声明“原版批次探针”：投料槽里还有原版材料能凑成游戏自己的批次时，注册配方让出该夜，凑不成批时才由 Forge 加工并跳过原生循环。液体配方可声明整叠投入：投料槽里价值最大的那一件优先，整叠保留到目标件数完成，完成时可按内容方声明只消耗应付的整件，余料留在槽里；价值由内容方按合并价值计算。液体配方还可声明 `ResolveWaterMillilitres` 按本批实际投入计算每件产物的用水毫升数（内容方据此实现按价值 1:1 扣水），并用 `ProgressVersion` 标记批次计价语义：版本不符或来自旧记录格式的进度会被清空并从槽内投入重新开批。液体产物同样在扣料前经过内容方的 `PrepareOutput` 收尾（例如灌装液体并校验读回），失败时整夜回滚。Forge 补充垃圾桶清理已停用，由原版清洁服务处理。本轮改动尚待游戏内过夜验证。本地开发打包默认日志级别为 `INFO`，详细探针须显式设为 `DEBUG`，用户的 `MelonPreferences.cfg` 设置优先。

---

## English

### Dependencies

- **Probably Stolen Demo**, Steam Build `25382790`
- [MelonLoader](https://melonwiki.xyz/)

### Installation

1. Copy `Nicokobo.Forge.dll` from the matching content mod bundle into the game's `Mods/` folder. Keep only one copy of Forge there; remove older Forge DLLs when updating.
2. Copy any content mod that requires Forge into `Mods/` as directed by that mod.
3. Launch the game. Forge has no standalone gameplay content or settings panel.

### What Forge provides

- A shared base for compatible mods to register items, facilities, nodes, machine modules, and effects.
- Machine and recipe registration, input admission, and nightly item or liquid processing for furnace-style machines. See [Machine API](docs/MACHINE_API.md).
- Integration points for a Nico Workshop with tabs from multiple mods.
- Shared support for content mods' start IDs and per-run data.

The items, upgrades, unlock costs, and workshop entries come from the content mods you install. Each content mod documents its own controls and requirements.

### License

Released under the [MIT License](LICENSE).

---

## 中文

### 依赖

- **Probably Stolen Demo**，Steam Build `25382790`
- [MelonLoader](https://melonwiki.xyz/)

### 安装

1. 将配套内容 Mod 包中的 `Nicokobo.Forge.dll` 复制到游戏的 `Mods/` 目录。更新时移除旧版 Forge DLL，确保目录中只保留一份。
2. 按对应内容 Mod 的说明，将需要 Forge 的 Mod 复制到 `Mods/`。
3. 启动游戏。Forge 本身没有独立的玩法内容或设置面板。

### Forge 提供什么

- 为兼容的 Mod 提供物品、设施、节点、机器模组和效果的共用注册基础。
- 为熔炉类机器提供物品及配方注册、投料判定和夜间固体／液体配方处理，调用方式见[机器 API](docs/MACHINE_API.md)。
- 提供可由多个 Mod 共用标签页的 Nico 工坊。
- 为内容 Mod 提供开局 ID 和周目数据的共用支持。

具体物品、解锁价格和工坊条目由安装的内容 Mod 提供；操作方式和额外依赖请查看相应 Mod 的说明。

### 许可证

本项目基于 [MIT 许可证](LICENSE) 发布。
