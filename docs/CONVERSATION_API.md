# 剧情来访、分支对话与阅读窗 API

更新：2026-10-09。Forge 0.6.34 本地候选；伪人 Mod 0.1.58 为首个调用者。离线检查与编译通过，原生交互、生命周期及备份恢复仍待实机验收。

## 职责

Forge 创建具名原生顾客、后果预览与选择菜单，持有原生委托和临时物品，管理原生阅读窗。内容 Mod 提供人物外观、文本、事件时机、选择资格与实际资源交易。三个 API 均不读写剧情文件、不调用 `SaveGame`、不改变原版保存时机。

查询 `ForgeDialogueApi.IsAvailable`、`ForgeClientVisitApi.IsAvailable`、`ForgeReadingApi.IsAvailable`，或 `ForgeCapabilities.Current` 的 `BranchingDialogue`、`ClientVisits`、`ReadingWindow`。当前三项共用完整的生命周期钩子集合；任一必需钩子失败则整体关闭，回滚已安装钩子。

## 分支对话

`ForgeDialogueApi.Create(ownerId, definition)` 创建托管只读定义，不立即建立原生对象。`ForgeDialogueDefinition` 包含：

- `Id`：以 `ownerId + "."` 开头的稳定定义 ID。
- `Speaker`、`Body`：已由内容方选定语言的角色名称和开场正文。
- `Choices`：每项包含唯一 `Id`、显示 `Label`、后果 `Preview` 和 `Confirm` 回调。
- `ConfirmLabel`、`BackLabel`、`LeaveLabel`、`LaterLabel`、`FailureText`：通用导航及异常提示的内容方文案。

选项列表在创建时冻结；原列表后续修改不会改变这次定义。原生流程为“开场 → 后果预览 → 确认 → 回应 → 离开”，也支持从预览返回以及稍后再说。当前不提供任意图结构的通用剧情引擎。

`Confirm(ForgeDialogueContext)` 在游戏线程同步执行，返回回应正文；上下文包含 `Generation`、`RunId`、`SlotId`、`OwnerId`、`RequestId`、`DialogueId`、`ChoiceId`。只对当前会话、当前到访者分派，每次来访最多调用一次确认；即使回调抛异常也不自动重做。内容方必须在回调中重新验证进度、钱款和物品归属。失败后需要新的一次预约，不在同一次会面反复执行交易。

## 原生来访

```csharp
ForgePresentationResult result = ForgeClientVisitApi.Queue(
    ownerId, requestId, runId, slotId, actorId, spriteName, script);
ForgePresentationResult current = ForgeClientVisitApi.Status(ownerId, requestId);
```

`actorId` 必须属于 owner 命名空间；`spriteName` 是内容方提供的原生人物外观资源名。Forge 创建只对话的、可离开但不可射击／逮捕的顾客，不复制原生模板的库存和交易回调。当前固定使用普通下层阵营，不增加角色交易业务。

只有当前匹配周目／槽位且商店营业时接受预约。请求以 `(ownerId, requestId)` 去重，同一个请求不会重新入队；同一 owner 的同一 actor 已排队、正在会面或结果不明时，新请求返回 `Conflict`。不同 owner 可使用相同 request 字符串。相同 request 换角色或定义 ID 同样冲突；相同角色／定义 ID 重提只返回原请求状态，不替换其文本或回调。

| 状态 | 含义 |
| --- | --- |
| `Queued` | 已观察到进入原生队列 |
| `Arrived` | 已观察到成为当前顾客 |
| `Dismissed` | 曾观察到的顾客已不在当前顾客或队列中；不代表剧情完成 |
| `Conflict` | 身份／请求不一致，或同一角色已有未完成请求 |
| `Unavailable` | 门控未开放、场景不就绪或不在营业中 |
| `Indeterminate` | 原生操作结果未确认；保留对象与请求，不自动重试 |
| `Invalid` | 参数或 owner 命名空间不合法 |

不会替换当前顾客。日历、阅读窗或工坊打开、原生对话进行中时不主动拉起下一位。退出这些界面后，在原生空闲状态请求正常接待。

原生队列没有被此 API 序列化。加载、开新周目、返回菜单使旧请求与回调失效；内容方从自己的存档恢复事件，并按需要重新预约。会面结束后仅在原生对话也不再引用该图时释放选择物品及委托；场景／加载边界交由原生销毁旧对象，不能用旧包装对象重新写入世界。

## 阅读窗

```csharp
ForgePresentationResult result = ForgeReadingApi.Open(
    ownerId, documentId, runId, slotId, title, body, signature);
ForgePresentationResult closed = ForgeReadingApi.Close(ownerId, documentId);
```

`documentId` 也必须属于 owner。使用临时 handnote 呈现，不把它放进玩家库存。已显示的相同 owner／document 可以刷新；另一个 owner、另一个文档或原版物品正在显示时，返回 `Conflict`，不覆盖。`Close` 只关闭当前归属一致的文档。

返回 `Opened` 表示观察到阅读窗已打开，不代表玩家已经读完或任何进度已经保存。内容方决定是否记录阅读。原生窗口关闭、改读其他物品或会话重置后，Forge 负责清理自己的临时对象。

## 存档与备份

继续使用 `ForgeRunDataApi.Stage` 把内容状态暂存到原版 `PlayerStore.modData`。本 API 不另建文件、不迁移已有数据键、不替代内容交易收据。完整 `.es3` 备份包含当时已由原版保存的内容状态，未保存的日中状态不在备份内。

## 验证范围

- 新增 24 项托管契约检查：定义冻结、owner 隔离、请求去重、未确认请求防重做、旧会话与双击回调、阅读窗占用。
- 原生适配元数据检查扩展至 173 项；`Build-P0.ps1` 的领域、生命周期、钩子保护和核心／样例编译通过。
- Aug 原有剧情领域检查与相关编译、菜单静态检查继续运行。
- 待原生验收：多内容 Mod 同时预约／阅读、真实人物呈现、确认回调、窗口替换、加载及菜单时旧回调失效、关闭后的原生引用释放、原版保存与备份恢复。离线检查不能代替这些结果。
