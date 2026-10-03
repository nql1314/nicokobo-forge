# 公共资源、存档读取与补丁检查

本页是 2026-10-03 审查后新增 API 的接入说明。内容 Mod 继续维护图片资源、物品字段、交易结果和玩法规则。

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

## 同帧库存效果读取（0.6.6）

`ForgeInventoryApi.CaptureOwnedItemCounts(store)` 返回自有且数量为正的物品 ID／数量只读字典。效果读取在同一帧和同一周目共用一份脱离原生句柄的快照；具体库存转移、数量修改、所有权标签与载入／新周目／日夜边界在前后回调中使快照失效。下一帧始终重新采集，捕获失败或捕获中发生变更时不发布可复用结果。整组原生观察钩子不可用时退回每次新采集。

内容 Mod 应传入自己关注的 ID 集合，原生所有权检查只覆盖已请求的 ID；新增请求会使快照失效，字典也可能包含其他读者已请求的 ID。省略 ID 集合时采集全部物品。配方、加成公式和启用条件仍归内容方。交易预检、夜间批次及保存边界继续使用 `CaptureRunItems`／生命周期上下文中的新采集，不能用效果快照证明交易或持久化。

`ForgeInventoryApi.IsItemDragActive` 只观察已存在的原生拖放处理器。工坊后台刷新等待拖动结束并静置 250 ms，逐件证明、保存与结局证据仍即时采集。

## 其他候选

| 现有实现 | 本轮处理与理由 |
| --- | --- |
| 机器批次、电量、液体和生产价值 | 已有专门 Forge API，继续复用 |
| 图集读取与发布 | 已提取；资源仍在内容程序集 |
| ES3 读取、语法和唯一字段查找 | 已提取；奖励、现金、声望和所有权比较仍在所属 Mod |
| 原生入口有效性检查 | 已开放；不替 Mod 安装玩法 Hook 或改变其构建门控 |
| 成就领奖、初始物资、购买补偿 | 保留业务流程；各自凭据、库存范围和保存恢复规则不同，先共享文件读取 |
| 义体分类、品质、熔炉优先级、成长公式 | 保留内容侧，属于玩法规则 |
| 热键屏蔽、文案、界面图案 | 窗口及无 Forge 依赖的 Mod 接入方式不同，暂不统一依赖 |
| 库存搬运、网络货物持久化 | 仍未开放；`InventoryTransfer=false` |

结果见[本轮审查记录](probes/2026-10-03-forge-review-shared-apis.md)。新调用者须使用本轮配套构建的 Forge DLL；历史 `0.6.5` 没有新增入口，不能只按版本字符串混用。
