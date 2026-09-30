# Nicokobo Forge / 模组前置框架

Author: Nicokobo · Version: 0.5.0

## Summary / 摘要

**English:** A shared dependency for Probably Stolen Demo content mods, supporting new items, effects, and the Nico Workshop. Forge adds no gameplay content by itself.

**中文：**Probably Stolen Demo 内容 Mod 的共用前置，支持新物品、效果与 Nico 工坊；单独安装不会增加玩法内容。

This local `0.5.0` candidate targets **Probably Stolen Demo Steam Build 25382790**. It replaces the previous machine API with native UI templates, item/liquid/mixed inputs, a single item or container output template, module and battery slots, and transactional nightly processing. Content mods must be rebuilt; old machine saves are not migrated. Local builds do not prove in-game UI, production or save/reload behavior.

当前 `0.5.0` 面向 **Probably Stolen Demo Steam Build 25382790**。机器 API 已替换为原版 UI 模板声明：支持物品、液体或两者同时输入，每台机器选择物品仓或容器输出，并配置模组仓、电池和手册槽。Forge 统一处理液体组分、动态体积、每批／每件耗电、仓库容量、逐项读回及失败恢复，每台机器每夜最多尝试一批。自定义机器复用两个公共生命周期 Hook；原版熔炉扩展按需加装两个钩子。合成扩展 K01–K05 和机械飞升的 K05 追加配方已迁移到新接口，需要配套重新编译，不提供旧接口兼容或旧存档迁移。游戏内 UI、过夜和新档重载待验证。本地默认日志为 `INFO`，用户配置优先。Forge 补充垃圾桶清理仍停用。

公共 API 已按注册、运行时、生命周期、模组、文本、库存、周目数据、电量和液体划分。机核协议的重复接入改为公共订阅，必要原生操作集中到适配器，说明与边界见 [API 职责](docs/API_BOUNDARIES.md)。配套候选为模组矩阵 1.1.0、物流脉络 0.2.0、合成扩展 0.8.0、机械飞升 0.1.8。

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
- Native machine UI templates, recipe registration, mixed item/liquid admission, item or container output, and atomic nightly resource processing. See [Machine API](docs/MACHINE_API.md).
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
- 提供机器 UI 模板、物品与液体并用输入、物品或容器输出及统一过夜事务，调用方式见[机器 API](docs/MACHINE_API.md)。
- 提供可由多个 Mod 共用标签页的 Nico 工坊。
- 为内容 Mod 提供开局 ID 和周目数据的共用支持。

具体物品、解锁价格和工坊条目由安装的内容 Mod 提供；操作方式和额外依赖请查看相应 Mod 的说明。

### 许可证

本项目基于 [MIT 许可证](LICENSE) 发布。
