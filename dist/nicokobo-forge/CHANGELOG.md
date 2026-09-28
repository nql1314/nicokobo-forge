# Changelog / 更新日志

What each released version of Nicokobo Forge changed. Install steps are in the [README](README.md).

各发布版本改了什么。安装步骤见 [README](README.md)。

## 0.1.0 — 2026-09-27

### English

**Added**

- A shared dependency for content mods to register game items, facilities, nodes, machine modules, and effects.
- Nico Workshop support, where multiple mods contribute their own tabs and Forge handles display and event dispatch.
- Shared start-ID claiming and per-run data support for compatible content mods.
- Safeguards for ID conflicts with existing game or mod entries: other extensions keep their results, and only the affected Forge capability is disabled.

**Notes**

- Targets Probably Stolen Demo Steam Build `25382790` and needs [MelonLoader](https://melonwiki.xyz/). Other game builds are unverified.
- Forge adds no gameplay content by itself.

### 中文

**新增**

- 内容 Mod 共用前置：注册游戏物品、设施、节点、机器模组与效果。
- Nico 工坊支持：多个 Mod 各自贡献标签页，Forge 只负责展示与事件分派。
- 为兼容的内容 Mod 提供开局 ID 认领与周目数据支持。
- 保护措施：与游戏或其他 Mod 条目 ID 冲突时，保留其他扩展的结果，只停用受影响的 Forge 能力。

**说明**

- 面向 Probably Stolen Demo Steam Build `25382790`，需要 [MelonLoader](https://melonwiki.xyz/)；其他游戏构建未验证。
- Forge 单独安装不增加玩法内容。
