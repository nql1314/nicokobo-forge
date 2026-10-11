# 公共资源、存档读取与补丁检查

本页记录当前公共服务的接入契约。内容 Mod 继续维护图片资源、物品字段、交易结果和玩法规则。

## 嵌入式图集

`ForgeAssetsApi.CreateEmbeddedSpriteAtlas` 返回进程内持有的 `ForgeSpriteAtlas`。声明不访问游戏对象；`TryInstall` 和 `Assign` 应在游戏线程、IL2CPP 初始化后调用。

```csharp
private static readonly ForgeSpriteAtlas Icons = ForgeAssetsApi.CreateEmbeddedSpriteAtlas(
    "example.content", "Example/Content/Icons", typeof(MyPlugin).Assembly,
    [new("example_card", "Example.Content.Assets.card.png", 32, 48)]);

// 从已有更新或目录就绪接入调用；原生缓存未就绪时稍后重试。
Icons.TryInstall(onInstalled: () => Log("icons ready"),
    onFailure: error => Warn(error.Message));
Icons.Assign(item, "example_card"); // 仅在安装成功后赋图。
```

- 声明会冻结列表；相同 owner、程序集、路径、定义和滤镜重复声明返回同一实例。被其他声明认领或原生缓存中已存在的路径会被拒绝，保留原内容。
- `Installed` 表示完整发布且逐张读回得到同一个 Sprite，`Count` 是安装成功后的图片数；它们不证明某件物品已显示图片。
- 缓存未就绪时返回 `false`，默认每 250 ms 可重试一次。资源缺失、尺寸错误、冲突和发布失败会进入终止的 `Failed` 状态。
- 写入后抛错同样进入恢复路径，只撤销本次发布。无法确认撤销时保留资源，避免缓存指向已销毁图片。通知回调抛错不会改变安装结果。
- 默认 `Point`，可显式指定 `Bilinear`。像素尺寸与物品占格独立，保持左上角锚点和 100 pixels-per-unit。
- `Assign(..., allowStaging: true)` 允许安装前暂存路径，失败状态仍拒绝赋图。暂存不证明解码或显示成功；不要跨周目缓存原生物品句柄。
- 图集按游戏进程持有，没有公开卸载接口，已发出的物品可能仍引用它们。

已迁移 Forge 名片、机械飞升的义体成品／神经接口／Jackson 纸条，以及矩阵和合成扩展图标，共六处。旧 `mods-melonloader/shared/EmbeddedSpriteAtlas.cs` 和 `UsesEmbeddedSpriteAtlas` 编译分支已移除。图片、路径、Sprite key、尺寸和各处滤镜保留原定义。

矩阵与神经接口保留原有的可选图片策略：公共安装失败后由内容侧保留声明路径／原版缺图表现，玩法继续；合成扩展等要求图集成功的工厂仍拒绝缺图。公共 API 不替内容方决定这一取舍。

## 生命周期事件

`ForgeLifecycleApi.Subscribe(ownerId, callbackId, phase, callback, order)` 同步分派，先按 `order` 升序，再按所属回调 ID 排序。订阅不抑制原版方法；一个回调失败不阻止其他订阅者。上下文的 `Items` 只在本事件内延迟捕获一次，仍包含实时原生句柄；写入前重新核对周目、归属与槽位，事件结束后不保留上下文。

| phase 与数值 | 原生边界及保证范围 |
| --- | --- |
| `BeforeLoad=0`、`BeforeNight=2`、`BeforeNightServices=4` | 表示调用意图，其他 prefix 拒绝原方法时仍可能执行；不能据此清除尚未切换周目的故障锁或扣费记录 |
| `AfterLoad=1`、`AfterNight=3`、`AfterNightServices=5`、`AfterSleep=6`、`AfterEndDay=7` | 对应原生 postfix 仅在 `__runOriginal=true` 时分派，表示原方法主体执行过 |
| `NightAttemptFinished=8` | 共用 `PlayerStore.EndNight` 的 postfix；到达该 postfix 即分派，包括原方法被 prefix 拒绝的情况。正常路径先分派业务 `AfterNight`，再清理 attempt 窗口 |
| `AfterGameLoadedLate=9` | 在真实 `ModHook.FireOnGameLoadedLate` 主体执行后的 postfix 分派，区别于 `LoadGame` 返回；供需要晚期载入通知的消费者使用 |

原值 0–7 保持不变，8／9 为追加值。`AfterLoad` 只证明 body-run，不证明 ES3 成功：缺文件分支可正常早返回并触发 AfterLoad，却没有 GameLoadedLate 通知。晚期通知本身也不替代业务快照与文件读回。`NightAttemptFinished` 不保证原方法抛异常后仍有通知，不能当作通用 `finally` 或资源提交证明。

当前工坊的 AfterLoad 回调先 Reset 再请求 Bind，机器同一回调先 reset 再 rebind；合成眼镜／熔炉及物流的加载重置也移到这一 body-run 边界，避免被拒 Load 清除活状态。真实文件保存、槽位与完整载荷仍由各业务方验收；本轮范围见[当前进度](FORGE_PROGRESS.md#2026-10-06-联合-review-候选)。

Matrix 在真实 `AfterGameLoadedLate` 只保留 store／run／slot 与已成功宿主的标量身份。active 后的 Update 重新捕获本周目，核对玩家所有权、正数量和真实模块槽关系，按宿主去重调用公开 `ForgeModuleApi.Recompute`；失败每秒 fresh capture 重试，成功宿主不重复处理，稳定帧不扫描库存。新载入代际及身份变化撤销旧请求，跨帧不保存物品／库存／事件上下文。candidate003 自动读档 24 台完整快照通过，属于调用者验收，不能把此结果扩大为生命周期事件的 ES3 成功保证。

## ES3 存档读取

`ForgeSaveReadbackApi` 不调用 `SaveGame`，也不反序列化游戏对象。返回独立 JSON 文档，必须在 `using` 范围内读取。

```csharp
using var saved = ForgeSaveReadbackApi.ReadFile(path,
    maxBytes: 16 * 1024 * 1024, maxDepth: 256);
if (saved.Status != ForgeSaveReadStatus.Readable) return false;
var wrapper = ForgeSaveReadbackApi.RequireProperty(saved.Root, "playerStore");
if (ForgeSaveReadbackApi.RequireProperty(wrapper, "__type").GetString()
    != "PlayerStore,Assembly-CSharp") return false;
var store = ForgeSaveReadbackApi.RequireProperty(wrapper, "value");
if (ForgeSaveReadbackApi.RequireProperty(store, "runID").GetString() != expectedRunId ||
    ForgeSaveReadbackApi.RequireProperty(store, "saveSlotId").GetInt32() != expectedSlot)
    return false;
var data = ForgeSaveReadbackApi.RequireProperty(store, "modData");
return ForgeSaveReadbackApi.RequireProperty(data, ownedKey).GetString() == expectedJson;
```

调用者还需处理字段缺失、重复或类型错误时的异常。交易涉及实物或余额时，应继续检查库存身份和数值，不能只匹配 `modData`。

| 入口或结果 | 契约 |
| --- | --- |
| `ReadFile` | 限制大小后分配，检查读取长度和文件元数据；支持共享模式的平台上禁止并发写入；失败不重试、不写文件 |
| `ReadBytes` | 检查已持有的 ES3 字节，限制大小、严格 UTF-8、对象根和嵌套深度 |
| `Parse` / `Normalize` | 兼容 ES3 数字键、字符串内原始控制字符及旧反斜线；保留值，不应用于普通 Mod JSON |
| `TryGetUniqueProperty` | `false` 表示非对象或重复字段；`true` 且 `present=false` 表示可选字段不存在 |
| `RequireProperty` | 缺失、重复或非对象时抛出 `InvalidDataException`，不会默默采用最后一个值 |
| `Readable` | 仅表示读取文本可解析；周目、槽位、业务 JSON、库存可达性与奖励凭据仍由调用者验证 |
| 其他状态 | `Missing`、`TooLarge`、`ChangedDuringRead`、`Malformed`、`Unavailable`、`Invalid` 分开报告 |

公共默认上限位于 `BuildConfig/ForgeNumbers.cs` 的 `SaveReadback`。Forge 成就显式保持 64 MiB／64 层，机械飞升保持 16 MiB／256 层；其业务比较规则未迁入公共层。文件读取不证明持久刷盘或新进程可以成功读档。

## 原生补丁目标检查

`ForgeHookApi.TryValidateNativeTarget(MethodInfo, out reason)` 复用 Forge 内部入口检查。在加载器初始化 IL2CPP 后、安装补丁前调用：

```csharp
if (!ForgeHookApi.TryValidateNativeTarget(target, out var reason))
    throw new InvalidOperationException(reason);
```

整组必需目标应先完成检查。它拒绝抽象／开放泛型、缺少生成方法元数据、空元数据指针及零执行地址。检查成功不证明回调签名、构建版本、补丁顺序或行为正确，补丁安装和恢复仍由调用者负责。

当前构建的 `GameInventory.Expel` 与 `UncheckedAccept` 是原生抽象声明，生成的互操作包装却可能呈现为普通方法。机械飞升工坊已改用 `GameGridInventory`、`GameSlotInventory`、`GameGridScrollableInventory` 和 `GameCharacterRaidInventory` 的具体实现，并使用此检查。

## 同帧库存效果读取

`ForgeInventoryApi.CaptureOwnedItemCounts(store)` 返回自有且数量为正的物品 ID／数量只读字典。同一周目的效果读取最多复用 250 毫秒的快照，不持有跨帧原生句柄；具体库存转移、数量修改、所有权标签、买卖、物品销毁与载入／新周目／日夜边界在前后回调中立即使快照失效。绕过这些入口的直接字段修改在快照过期后观察到；交易预检仍须使用新采集。捕获失败或捕获中发生变更时不发布可复用结果。整组原生观察钩子不可用时退回每次新采集。

内容 Mod 应传入自己关注的 ID 集合，原生所有权检查只覆盖已请求的 ID；新增请求会使快照失效，字典也可能包含其他读者已请求的 ID。省略 ID 集合时采集全部物品。配方、加成公式和启用条件仍归内容方。交易预检、夜间批次及保存边界继续使用 `CaptureRunItems`／生命周期上下文中的新采集，不能用效果快照证明交易或持久化。

`ForgeInventoryApi.IsItemDragActive` 只观察已存在的原生拖放处理器。工坊后台刷新等待拖动结束并静置 250 ms，逐件证明、保存与结局证据仍即时采集。

## 整批单件物品落位预检

`ForgeInventoryPlacementApi.IsAvailable` 同时检查当前游戏构建、`WholeTransferPreview` 与几何接口签名。`PlanWholeGrid(GameGridInventory, IReadOnlyList<GameItem>)` 返回只读 `ForgeInventoryPlacement` 列表（原物品与克隆形状）；不可用、材料无效、原生准入拒绝或整批放不下时返回 `null`，空批次返回空列表。

输入必须是未附着、数量为 1 且指针互不重复的物品，不支持堆叠或拆分。预检读取库存形状与已有物品占格，在托管占格图中依原生行列／旋转／镜像顺序预留整批，再对真实库存调用 `TryInventorySlot` 检查准入、非叠加目标与单件接纳。它不创建临时原生库存，不修改输入物品的 ID、形状、父库存或数量，也不占用真实仓位。

Forge 内置成就和机械飞升共用该预检；正式接纳前仍须重查落点，交付、失败恢复、领取凭据与保存由各调用者负责。方案成功不证明后续接纳、回滚或保存成功，也不改变 `InventoryTransfer=false`。

## 其他候选

| 现有实现 | 本轮处理与理由 |
| --- | --- |
| 机器批次、电量、液体和生产价值 | 已有专门 Forge API，继续复用 |
| 两个内容 Mod 的整批附加成本算术 | 0.6.15 增加 `PerOutputWithOverhead`；材料倍率后加整批成本，按产量分摊并只取整一次。电价和基础耗电选择仍在内容方 |
| 图集读取与发布 | 已提取；资源仍在内容程序集 |
| ES3 读取、语法和唯一字段查找 | 已提取；奖励、现金、声望和所有权比较仍在所属 Mod |
| 原生入口有效性检查 | 已开放；不替 Mod 安装玩法 Hook 或改变其构建门控 |
| 成就领奖、初始物资、购买补偿 | 保留业务流程；各自凭据、库存范围和保存恢复规则不同，先共享文件读取 |
| 义体分类、品质、熔炉优先级、成长公式 | 保留内容侧，属于玩法规则 |
| 热键屏蔽、文案、界面图案 | 窗口及无 Forge 依赖的 Mod 接入方式不同，暂不统一依赖 |
| 单次库存搬运／倒液、网络货物持久化 | 已有 `ForgeItemTransferApi.Move` / `ForgeLiquidTransferApi.Pour`；完整网络待实现，`InventoryTransfer` 总能力仍为 `false`；见[物流边界](LOGISTICS_API_PROGRESS.md) |

这些公共服务的较早审查见[2026-10-03 记录](probes/2026-10-03-forge-review-shared-apis.md)，结论仅适用于该记录注明的产物。新调用者须使用当前配套构建的 Forge DLL，不能只按版本字符串混用。

## 窗口指针捕获

Forge `0.6.8` 提供纯状态类 `ForgeWindowInputCapture`，供 Nico 工坊和合成扩展手册共用。内容方维护可见窗口的实际屏幕范围、UI 射线遮挡和原生补丁所有权。

- 打开和关闭分别调用 `Open(frame, mouseHeld)`、`Close(frame, mouseHeld)`；同帧重复关闭不会延长捕获。
- 输入回调查询 `BlocksPointer(frame, pointerInside, mouseHeld)`。窗口内的手势及关闭点击保护至释放当帧，窗口外开始的拖拽保留自己的释放事件。
- 离开场景时调用 `Reset()`。原生手册、库存选中状态和实际拖拽清理由调用方处理；释放到窗口范围内时须取消原生放置，不能仅依赖普通 Image 遮挡库存目标。
