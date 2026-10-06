# Nicokobo Forge / 模组前置框架

Author: Nicokobo · Package version: **0.6.26**

Probably Stolen Demo 内容 Mod 的共用前置，提供物品／效果注册、机器模板、资源事务与 Nico 工坊。Forge 内置全开局名片和 10 项原版成就，支持周目进度与一次性领奖；按 N 或双击名片打开。只使用原版成就时可单独安装 Forge，其他玩法由内容 Mod 提供。

新游戏选择栏使用自适应的紧凑行高，原版和 Mod 选项共用；内容超出时可用鼠标滚轮或拖动细滚动条查看。

原版档案箱内部容量扩为 16×16（256 格），新游戏和已有存档共用，保留原有物品、位置与收纳规则。

## 安装 / Installation

1. 游戏需已安装 [MelonLoader](https://melonwiki.xyz/)。本系列开发目标为 Demo Steam Build `25382790`。
2. 将 `Nicokobo.Forge-0.6.26.dll` 与需要的配套内容 Mod DLL 复制到游戏 `Mods/`。
3. 更新时移出旧版本 DLL，Forge 与每个 Mod 各只保留一份；内容 Mod 应与 Forge 来自同一次配套构建。

Forge includes the Nico Workshop card for all starts and ten native-game achievements with per-run progress and one-time rewards. Press N or double-click the card to open the workshop. Forge can be installed alone for these achievements.

The new-game selection list uses compact, adaptive rows for native and mod options. When the list overflows, use the mouse wheel or drag the thin scrollbar.

The native dossier storage expands to 16×16 (256 cells) for new and existing saves, preserving its items, positions and admission rules.

Install MelonLoader, then copy `Nicokobo.Forge-0.6.26.dll` and matching content mod DLLs into the game's `Mods/` folder. Keep one copy of each assembly and update the dependency together with its content mods.

界面操作、额外依赖和实机验收以各内容 Mod 的说明为准。默认日志为 `INFO`，`UserData/MelonPreferences.cfg` 中用户已有设置优先；详细诊断需显式 `DEBUG`，修改后重启。

Controls, additional dependencies, and gameplay validation are documented by each content mod. The packaged default log level is `INFO`; existing user preferences take precedence.

本项目以 MIT 许可证发布。 / Released under the MIT License.
