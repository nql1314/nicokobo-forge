# Nicokobo Forge 0.1.0 / 模组前置框架 0.1.0

Author: Nicokobo · Target: Probably Stolen Demo Steam Build `25382790`

## English

### Initial preview release

- Adds a shared dependency for content mods to register game items, facilities, nodes, machine modules, and effects.
- Adds integration points for custom Wilds Network upgrades and multi-mod Nico Workshop tabs.
- Adds shared start-ID and per-run data support for compatible content mods.
- Includes safeguards for failed custom upgrade purchases and ID conflicts with existing game or mod entries.

### Install

Install [MelonLoader](https://melonwiki.xyz/), then place **one** `Nicokobo.Forge.dll` in the game's `Mods/` folder alongside any content mod that requires it. Forge does not add gameplay content by itself. See the [README](README.md) for details.

**Preview status:** Core loading and sample effect registration have been checked locally. Custom upgrade purchases, workshop interaction, and save reloads still need broader in-game verification. Back up saves before testing content mods that use these features. Other game builds are unverified.

---

## 中文

### 首个预览版本

- 为内容 Mod 提供游戏物品、设施、节点、机器模组和效果的共用注册基础。
- 提供自定义 Wilds Network 升级和多 Mod 共用的 Nico 工坊标签页接入。
- 为兼容的内容 Mod 提供开局 ID 和周目数据的共用支持。
- 为自定义升级购买失败及与游戏或其他 Mod 现有条目的 ID 冲突增加保护。

### 安装

安装 [MelonLoader](https://melonwiki.xyz/) 后，将**一份** `Nicokobo.Forge.dll` 放入游戏 `Mods/` 目录，并按需要安装依赖它的内容 Mod。Forge 单独安装不会增加玩法内容。详情见 [README](README.md)。

**预览版状态：**已在本地确认核心加载及示例效果注册。自定义升级购买、工坊交互和存档重载仍需进一步游戏内验证。测试使用这些功能的内容 Mod 前请备份存档；其他游戏构建尚未验证。
