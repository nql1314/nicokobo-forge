# Nicokobo Forge / 模组前置框架

Author: Nicokobo · Version: 0.1.0

## Summary / 摘要

**English:** A shared dependency for Probably Stolen Demo content mods, supporting new items, effects, Wilds Network upgrades, and the Nico Workshop. Forge adds no gameplay content by itself.

**中文：**Probably Stolen Demo 内容 Mod 的共用前置，支持新物品、效果、Wilds Network 升级与 Nico 工坊；单独安装不会增加玩法内容。

This is an early `0.1.0` build for **Probably Stolen Demo Steam Build 25382790**. Other game builds are unverified; Forge disables native features when the game build or required signatures do not match. Back up saves before trying content mods that use new upgrade or workshop features.

当前 `0.1.0` 是面向 **Probably Stolen Demo Steam Build 25382790** 的早期版本。其他游戏构建尚未验证；构建或必要签名不匹配时，Forge 会停用对应原生功能。体验使用新升级或工坊功能的内容 Mod 前，建议备份存档。

---

## English

### Dependencies

- **Probably Stolen Demo**, Steam Build `25382790`
- [MelonLoader](https://melonwiki.xyz/)

### Installation

1. Copy `Nicokobo.Forge.dll` into the game's `Mods/` folder. Keep only one copy of Forge there; remove older Forge DLLs when updating.
2. Copy any content mod that requires Forge into `Mods/` as directed by that mod.
3. Launch the game. Forge has no standalone gameplay content or settings panel.

### What Forge provides

- A shared base for compatible mods to register items, facilities, nodes, machine modules, and effects.
- Integration points for custom Wilds Network upgrades and a Nico Workshop with tabs from multiple mods.
- Shared support for content mods' start IDs and per-run data.

The items, upgrades, unlock costs, and workshop entries come from the content mods you install. Each content mod documents its own controls and requirements. See [release notes](release.md) for this version.

### License

Released under the [MIT License](LICENSE).

---

## 中文

### 依赖

- **Probably Stolen Demo**，Steam Build `25382790`
- [MelonLoader](https://melonwiki.xyz/)

### 安装

1. 将 `Nicokobo.Forge.dll` 复制到游戏的 `Mods/` 目录。更新时移除旧版 Forge DLL，确保目录中只保留一份。
2. 按对应内容 Mod 的说明，将需要 Forge 的 Mod 复制到 `Mods/`。
3. 启动游戏。Forge 本身没有独立的玩法内容或设置面板。

### Forge 提供什么

- 为兼容的 Mod 提供物品、设施、节点、机器模组和效果的共用注册基础。
- 提供自定义 Wilds Network 升级接入，以及可由多个 Mod 共用标签页的 Nico 工坊。
- 为内容 Mod 提供开局 ID 和周目数据的共用支持。

具体物品、升级、解锁价格和工坊条目由安装的内容 Mod 提供；操作方式和额外依赖请查看相应 Mod 的说明。本版更新内容见[发布记录](release.md)。

### 许可证

本项目基于 [MIT 许可证](LICENSE) 发布。
