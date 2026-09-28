# Nico 工坊解锁星图

本地设计与实现，2026-09-27。参考 [ATM10 的 FTB Quests 任务图](https://github.com/AllTheMods/ATM-10/blob/main/config/ftbquests/quests/chapters/chapter_2_the_star.snbt) 的坐标、依赖、条件和奖励数据模型；本工坊使用自己的 IMGUI 界面，没有复制游戏包资源。

## 结构

一个内容提供者通过 `ForgeWorkshopApi.RegisterChain` 注册一条链。基础游戏适配 Mod、独立内容 Mod、整合包目标都可各注册一条；是否存在“总链”由提供者决定，不强制把每个 Mod 的节点并入同一条链。原有 `RegisterTab` 继续可用，且在界面里同样是一条链。当前最多注册 16 条链，界面每页显示 6 条；每条链的快照上限为 20 个节点、5 组且每组最多 4 个节点。

每条链提供 `ForgeWorkshopSnapshot`，内含可按分组自动排成放射状的节点。节点可通过 `GraphX` / `GraphY` 配置 `[-1, 1]` 范围内的位置；节点间用稳定 ID 声明 `Dependencies`，同链前置会画连线。跨链前置也能写入依赖列表；Forge 在同一轮已校验快照中要求每个前置 ID 唯一存在且处于已解锁状态，缺失 Mod 或重复 ID 都会阻止点击。当前视图只显示同链连线。一个总链可以引用各内容链的完成标记；所属 Mod 在实际解锁回调中仍要重新核对这些状态。

## 条件和奖励

`Conditions` 是“全部满足”的列表，可以混合信用点和多种物品。每项有数量、当前是否满足，以及 `Consume`：`true` 表示解锁时扣除，`false` 表示仅检查持有。物品使用稳定 `ResourceId`，可填当前语言的 `DisplayName`。`Rewards` 列出解锁成功后给予的信用点或物品。没有条件的节点可以免费解锁。

```csharp
var node = new ForgeWorkshopEntry(
    id, title, subtitle, description, prerequisiteText, "", "", stateText,
    actionText, unlocked, prerequisiteMet, actionEnabled)
{
    Dependencies = ["base.power", "example.mod.research"],
    GraphX = 0.25f,
    GraphY = -0.4f,
    Conditions =
    [
        new(ForgeWorkshopResourceKind.Credits, "", 5000, true, credits >= 5000),
        new(ForgeWorkshopResourceKind.Item, "scrap_metal", 3, true, scrap >= 3),
        new(ForgeWorkshopResourceKind.Item, "rare_key", 1, false, keys >= 1)
    ],
    Rewards =
    [
        new(ForgeWorkshopResourceKind.Credits, "", 1000),
        new(ForgeWorkshopResourceKind.Item, "example.mod.reward", 1)
    ]
};
```

Forge 验证快照结构、显示状态、阻止明显不满足条件的点击，并把点击分派给所属链的 `unlock(id)`。快照是展示数据，可能在点击前失效。内容提供者必须在回调内按当前周目重新确认前置、条件与奖励资格；再执行扣除、奖励、效果和持久化，失败时恢复或封锁继续交易。奖励只在成功交易中发放一次，已解锁的节点不可再次领取。Forge 当前没有安全的原生物品写入与跨库存事务 API，所以 `Conditions` 和 `Rewards` **不会自动改动游戏资源**。

现有机械飞升内容链已经迁移为星图：四条义体分支汇入中间的最终节点；信用点与材料均标为消耗，原有内容侧购买和存档读回逻辑继续执行。工坊探针注册一条测试链和一条跨链共同目标，用本次运行的内存状态测试界面与事件分派，不进行游戏资源交易。

## 验收边界

- 已实现：链注册、链分页、同链依赖连线、自动放射布局与手工坐标、条件/消耗/奖励展示及点击门控。
- 编译可以验证 API 与调用方；游戏内布局、点击、资源交易、奖励发放、存档回读和重载需要分别测试。奖励写入在具体内容 Mod 实现后才能验收。
