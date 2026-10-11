# 剧情来访、分支对话与阅读窗 API

`ForgeDialogueDefinition.AllowLater` 默认 `true`；设为 `false` 时，来访和电话的初始选项不追加 `LaterLabel`，选择后的结束按钮仍保留。内容方可用于无需提交资源的线性对话；伪人 E02、E04、E05、E09 使用此设置。

更新：2026-10-11。Forge 0.6.35 本地候选；伪人 Mod 为首个调用者。离线检查与编译通过，新增直接选择、电话及供货的原生交互、生命周期与存读档仍待实机验收。

## 职责

Forge 创建具名原生顾客、后果预览与选择菜单，持有原生委托和临时物品，管理原生阅读窗。内容 Mod 提供人物外观、文本、事件时机、选择资格与实际资源交易。三个 API 均不读写剧情文件、不调用 `SaveGame`、不改变原版保存时机。

查询 `ForgeDialogueApi.IsAvailable`、`ForgeClientVisitApi.IsAvailable`、`ForgeReadingApi.IsAvailable`，或 `ForgeCapabilities.Current` 的 `BranchingDialogue`、`ClientVisits`、`ReadingWindow`。当前三项共用完整的生命周期钩子集合；任一必需钩子失败则整体关闭，回滚已安装钩子。

## 分支对话

`ForgeDialogueApi.Create(ownerId, definition)` 创建托管只读定义，不立即建立原生对象。`ForgeDialogueDefinition` 包含：

- `Id`：以 `ownerId + "."` 开头的稳定定义 ID。
- `Speaker`、`Body`：已由内容方选定语言的角色名称和开场正文。
- `Choices`：每项包含唯一 `Id`、显示 `Label`、后果 `Preview` 和 `Confirm` 回调。
- `ConfirmLabel`、`BackLabel`、`LeaveLabel`、`LaterLabel`、`FailureText`：通用导航及异常提示的内容方文案。
- `RequireConfirmation`：可选 init 属性，默认 `true`。设为 `false` 时，点击选项直接执行 `Confirm` 并展示回复，不创建预览、确认和返回菜单。
- `NextPageLabel`、`PreviousPageLabel`：来访菜单超过 4 个按钮时的分页文案，默认 `More choices`／`Previous choices`；供应商定义也提供这两个属性。暂缓、离开等也计入总数。需要时从原生第三个按钮克隆第四个并登记到原生列表；4 个以内直接显示，超过 4 个才预留翻页按钮。

选项列表在创建时冻结；原列表后续修改不会改变这次定义。默认原生流程为“开场 → 后果预览 → 确认 → 回应 → 离开”，也支持从预览返回以及稍后再说；`RequireConfirmation = false` 的流程为“开场 → 选择并执行 → 回应 → 离开”。两种方式共用一次性提交与当前会话检查。原构造函数签名保持不变，已有调用者仍默认显示确认；直接选择的 `Preview` 可传空字符串，导航文案仍按原契约提供。当前不提供任意图结构的通用剧情引擎。

商店、供应商与电话的每个按钮都持有 `PLAYER` 选项节点：短标签写入该节点，`nextDialogue` 指向真正的预览／回复／返回菜单。原生选择后先前进到下一节点；不能把预览直接当成选项节点，否则会跳过预览并结束对话。离开按钮同样保留可显示的非空节点，其下一句为空。

来访与供应商菜单按当前 `DialogUIManager.dialogChoiceBubbles.Count` 分页，包含稍后、继续或离开选项在内，每页都不超过原生容量。分页保留原选项节点、交易回调和会话门控，切页仅导航，不提交交易；后续页可以返回前页。Build `25382790` 的原生界面只有三个按钮，直接提供四个以上选项会在 `DisplayChoice` 越界。电话菜单不使用此来访分页适配。

`Confirm(ForgeDialogueContext)` 在游戏线程同步执行，返回回应正文；上下文包含 `Generation`、`RunId`、`SlotId`、`OwnerId`、`RequestId`、`DialogueId`、`ChoiceId`。只对当前会话、当前到访者分派，每次来访最多调用一次；即使回调抛异常也不自动重做。内容方必须在回调中重新验证进度、钱款和物品归属。失败后需要新的一次预约，不在同一次会面反复执行交易。

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

## 电话联系人与多笔供货（2026-10-10 本地源码）

`ForgeSupplierPage.AutomaticOfferId` 是默认 `null` 的可选声明。仅适用于 `RequireConfirmation = false` 的直接选择页，ID 必须属于本页的 `Offers`；`AutomaticDelaySeconds` 默认 `0.6`，须为有限正数。内容方按当前剧情与资源决定是否声明，`Capture` 本身不执行交易。

自动选择从原生 `DisplayChoice` 实际显示目标所在页及按钮之后开始计时。可见性核对当前页、目标按钮与遮挡窗口，不依赖原生未维护的 `isConversationActiveFlag`。等待期间，仅该 owned 页面拦截 `SelectChoice` 的手动输入；到时调用原生 `SelectChoice`，沿用同一 `Purchase` 回调与一次性提交门控，自动展示回复。排队、构建页面、其他顾客、其他菜单、阅读窗或日历遮挡期间不执行；隐藏后重新显示须重新等待。加载、新周目和返回菜单清除原生会话及计时。一次尝试结束后不会自动重试；失败的资源处理和是否允许继续仍由内容方决定。其他供应商与默认页面不受影响。新增接口须使用配套编译的 Forge 与内容 Mod；原生演出节奏仍待实机验收。

`ForgeSupplierDefinition.AllowMenuRefresh` 是默认 `false` 的可选 init 属性。设为 `true` 后，当前菜单增加 `ContinueLabel` 按钮，即使没有可选交易也能重新调用 `Capture`；适合玩家在库存中修好实物后继续同次会面。刷新不执行 `Purchase`，与该页交易共用一次性选择门控；旧页刷新、旧交易与重复点击不能重建第二次菜单或执行资源操作。构造函数与其他供应商的默认导航保持不变。

`ForgeSupplierDefinition.CanContinue` 是可选的内容状态判断，默认 `null` 沿用可重复挑选流程。提供回调后，菜单刷新、交易回复与继续点击都会重检；返回 `false` 时，回复保留已经建立的离开选项，移除继续入口。回调只判断当前会面是否还能继续，不执行交易；终止条件由内容 Mod 持有。伪人 E07 在选择确认后停止继续，未修复或交付失败时仍可继续。

`ForgePhoneApi.IsAvailable`／`ForgeCapabilities.Current.PhoneContacts` 表示原生电话钩子集合已安装。`ForgePhoneApi.Register(owner, ForgePhoneContact)` 声明 owned 稳定 ID、电话号码、`Visible`、`DisplayName` 和 `Call` 回调，按 Mod 安装生命周期登记一次；同 owner／ID 重复登记返回 `Conflict`，不替换回调。当前成功登记返回 `Queued`，这里只表示声明被接受；联系人进入当前 `PlayerStore.PhoneClientDict`、电话簿显示和实际拨通仍须分别观察。号码冲突保留原联系人，并停用冲突声明。内容方在 `Visible` 中核对周目与解锁事实；`Call` 返回现有 `ForgeDialogueScript`，通过原生拨号、预览和挂断流程执行，每次电话最多一次确认。加载、新周目、菜单使旧回调失效；原生联系人使用 `None` 类型、`Regular` 状态，不接入原生供应商的库存或冷却服务。

`ForgeSupplierApi.Queue(owner, request, run, slot, actor, sprite, definition)` 创建可以多次挑选的独立来访，不改变 `ForgeDialogueApi` 每场仅一次提交的契约。`ForgeSupplierDefinition.Capture` 返回当前正文与 `ForgeSupplierOffer` 列表，后者包含 stable ID、标签、数量／总价预览与 `Purchase` 回调；内容方持有库存、价格、空间校验、扣款、交付和保存。购买流程为“货单 → 预览 → 确认 → 回应 → 继续挑选／送客”；继续挑选重新捕获新货单，每个旧货单只接受一次确认，旧按钮与双击不能再次执行购买。`Dismiss` 在明确送客时调用，内容方也应观察 `Dismissed` 处理其他离店方式。通用框架不刷新货量、不扣钱、不写 Mod 数据。

预约日期及营业调度由内容方使用原生日历和上述队列组合，原生队列仍不序列化；加载后从保存预约恢复，并在新会话重新排队。新接口尚未原生验收，不能以编译替代电话簿、连购、日夜边界与存读档验证。

电话脚本同样遵循 `ForgeDialogueDefinition.RequireConfirmation`。`ForgeSupplierDefinition` 也提供默认 `true` 的 `RequireConfirmation` init 属性；设为 `false` 时，从货单选项直接执行 `Purchase` 并展示回应，继续按钮仍重新捕获货单并创建新的提交门控，每个旧货单只能执行一次。伪人剧情的主线、核心业务、E07 修复和灰线电话均选择直接执行模式。

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

- 当前 91 项离线契约与跳转检查：定义冻结、默认确认兼容、直接选择、owner 隔离、请求去重、未确认请求防重做、旧会话与双击、阅读窗占用、选项预览／确认／回复／返回／离开及供货继续。直接选择覆盖首选即结算与显示回复、重复／其他选项不重复扣款发物品、旧会话拒绝和离开。跳转端口依据本地商店与电话反汇编。
- 原生适配元数据检查扩展至 186 项；`Build-P0.ps1` 的领域、生命周期、钩子保护和核心／样例编译通过。
- Aug 原有剧情领域检查与相关编译、菜单静态检查继续运行。
- 待原生验收：多内容 Mod 同时预约／阅读、真实人物呈现、确认回调、窗口替换、加载及菜单时旧回调失效、关闭后的原生引用释放、原版保存与备份恢复。离线检查不能代替这些结果。
