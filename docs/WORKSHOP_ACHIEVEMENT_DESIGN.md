# Nico工坊成就解锁设计方案

更新日期：2026-10-04。状态：已接入 Forge 0.6.11 源码，游戏内操作和存档重载待验收。

Forge 提供全开局共用的 Nico工坊名片和原生成就页。本方案保留 9 项高难度挑战与 1 项原版通关成就，条件和奖励按当前目录执行。

## 名片和入口

- 所有新周目获得一张 Nico工坊名片，由 Forge 统一注册和发放。保留现有 2×1 名片模板、图标与不可出售属性。
- 双击名片或按 N 打开工坊；名片不在背包中时，N 键仍可使用。容量不足时延迟发放，已发放的名片不重复补发。
- “原版”对所有开局开放。“伪人：机械飞升”保留为独立页面，按对应开局资格开放。

名片由 Forge 使用独立 ID `nicokobo.forge.nico_card` 注册，不兼容旧名片 ID 和旧发放记录。

## 页面布局

| 分组 | 节点数 | 内容 |
| --- | --- | --- |
| 产业成果 | 4 | 一桶纯水、巨鼠、完美品质金属锭、酿酒大师 |
| 收藏与装备 | 2 | 全套拾荒工具、满级拾荒扫描仪 |
| 经营与声望 | 3 | 十万家业、千分名店、派系名望 |
| 原版通关 | 1 | 立业有成 |

三条分支围绕原版通关成就展开，共 10 个节点。各项成就独立判定，连线只表示主题；通关成就以原版成功结局为条件。

单击查看条件、当前进度、奖励和领取状态；双击已达成节点或点击“领取奖励”领奖。X、Esc 和 N 关闭窗口。

## 成就与奖励

### 产业成果

| 拟定 ID | 成就 | 完成条件 | 一次性奖励 |
| --- | --- | --- | --- |
| pure_water_jug | 一桶纯水 | 拥有一只装满的原生桶装水，实际水质达到原生纯水档 | 高级滤水器 |
| rat_800g | 巨鼠 | 拥有一只存活的原生老鼠，单只实际重量达到或超过 800g | 牲畜免疫血清 x5；牲畜类固醇血清 x5 |
| quality_overload | 极限调校 | 拥有一块实际达到原生完美品质标准的金属锭 | 神经核心模组(受限) |
| master_brewer | 酿酒大师 | 拥有一瓶陈化 10 天以上的琼浆玉液荧光酒（至少 10 个游戏日） | 酵母生物反应器 |

纯水同时检查同一只桶的实际水质和满桶容量。老鼠按单只真实重量判断。金属锭采用原生完美档判定，不额外限制金属种类；对应原生纯度标识为 `INGOT_PURITY_PERFECT`。以上均为持有目标，可通过原生玩法正常获得。

酿酒大师要求同一瓶酒的实际品质为琼浆玉液荧光酒，且原生陈化天数达到 10；品质与陈化时间均读取实际状态。

### 收藏与装备

| 拟定 ID | 成就 | 完成条件 | 一次性奖励 |
| --- | --- | --- | --- |
| scavenger_toolset | 拾荒达人 | 同一时点持有全部 5 种原生拾荒工具，每种至少一件 | 背包(大) |
| max_scav_scanner | 顶配拾荒仪 | 持有通过原生升级达到上限的拾荒扫描仪，双份收获概率值达到 100 | 拾荒者信物 x1 |

拾荒工具清单：

| 原生 ID | 工具 |
| --- | --- |
| flashlight | 手电筒 |
| screwdriver | 螺丝刀 |
| wire_cutter | 剪线钳 |
| welder | 等离子切割机 |
| metal_scanner | 拾荒扫描仪 |

工具须实际归玩家所有且同时持有，背包和受支持的原生仓储均可计入。扫描仪读取原生 `SCAV_SCANNER_DOUBLE_CHANCE` 状态。

### 经营与声望

| 拟定 ID | 成就 | 完成条件 | 一次性奖励 |
| --- | --- | --- | --- |
| wealthy_store | 十万家业 | 原生总资产估值超过 100,000 信用点 | 机器区(扩建) |
| store_attractiveness | 千分名店 | 原生当前店铺吸引力达到或超过 1,000 | 存储箱(扩建) |
| faction_max_rep | 派系名望 | 任意一个可计原生派系的实际声望达到上限 200 | 走私者暗格(改进) |

资产按原生 `GetTotalEstimatedValue` 判断，恰好 100,000 尚未达成；吸引力按 `GetCurrentStoreAttractiveness` 判断。派系名单与声望字段在实施前固定。

### 原版通关

| 拟定 ID | 成就 | 完成条件 | 一次性奖励 |
| --- | --- | --- | --- |
| workshop_master | 立业有成 | 当前周目完成原版店铺买断通关：直接买断店铺，或还清房贷并触发成功结局 | 通关纪念徽章；本周目所有 NPC 收购预算×2 |

通关以当前周目触发原生 `buyout` 或 `buyout_mortgage` 成功结局为准，开始按揭不算完成。原版结算后选择继续游玩，可返回工坊领取徽章。

徽章是当前周目的工坊展示标记，不占库存。

领奖记录保存并读回确认后，当前顾客的剩余收购预算翻倍，后续 NPC 在预算初始化或到店时翻倍一次。成交按实际余额正常扣款，重复保存、读取预算或到店不再次翻倍；重新设置预算时按新金额计算。原生不限额顾客保留不限额规则。效果可与伪人原有预算×2叠加，合计×4。

该效果沿用当前周目的 `workshop_master` 已领取记录。已领取徽章的旧存档载入并确认记录后自动启用，无需再次领奖；新周目和未领取状态不启用。

## 奖励定义

| 奖励 | 数量 | 原生 ID |
| --- | --- | --- |
| 高级滤水器 | 1 | water_filter_adv |
| 牲畜免疫血清 | 5 | serum_green |
| 牲畜类固醇血清 | 5 | serum_red |
| 背包(大) | 1 | backpack_large |
| 拾荒者信物 | 1 | scav_token |
| 神经核心模组(受限) | 1 | system_capped_neural_core |
| 酵母生物反应器 | 1 | wine_yeast_infinite |
| 机器区(扩建) | 1 | machine_bay_ext |
| 存储箱(扩建) | 1 | storage_bay_large（原生名称：储藏区（扩建）） |
| 走私者暗格(改进) | 1 | smuggler_bay_mod |
| 通关纪念徽章 | 1 个标记 | 工坊展示记录 |
| NPC 收购预算×2 | 本周目持续效果 | workshop_master 已领取记录 |

扩建与改进类奖励按对应原生物品交付，保留原生容量与功能。

## 达成和领取规则

- 进度与奖励归当前周目，保存达成事实、领取状态及必要的物品证明。Steam 账号历史不代替周目判定。
- 持有目标读取同一时点的实际状态。达成不消耗证明物品，保存确认后使用或出售也不撤销。
- 达成与领取分别显示，每个节点只发奖一次。容量不足时保留领取资格，整理空间后再领。
- 交付前检查整份奖励，交付后将资源与领取记录一起保存并读回确认。中断时恢复原事务，避免重复发奖。
- 通关成就记录当前周目的原版成功结局，与其余 9 项挑战的完成和领奖状态独立。某项适配不可用时显示原因，保留已有记录。

## 原生适配

- 纯水：`water_jug` 的实际组分总量等于 `LIQUID_CONTAINER_CAPACITY`，且原生 `GetPurityArrayIndex(GetWaterPurity(item)) == 0`。
- 老鼠：存活状态与 `ANIMAL_WEIGHT_TAG.valueFloat` 的实际克数；金属锭：`GetPurity(item) == INGOT_PURITY_PERFECT`。
- 酒：原生发酵完成、非假酒、最高品质档 5、`AgableHelper.GetAge(item) >= 10`。自酿成品保留 `wine_bottle` ID，按同一物品的实际状态判定。
- 派系：下层区、上层区、治安部、黑市、革命军、卡特尔；读取 `GetReputationExact`，保留原生有效派系检查。
- 原版成功结局：观察当前周目 `ExecuteGameOver` 的 `buyout` 或 `buyout_mortgage`。
- 收购预算：在 `SetBudget` 两个重载、`SetClientBudget` 完成原生赋值后用 `OverrideBudget` 写入翻倍余额；`CreateClientInstance` 补齐领奖前已生成的来访者。同一预算只处理一次，原生 `ModBudget` 扣款保持原值。

本地构建、成就判定与存档格式检查结果见[原生成就本地验证](probes/2026-10-02-native-workshop-implementation-validation.md)。正式发布前验证目标可达性、两条通关路径、继续游玩后的领奖、满背包处理和保存重载。

参考：[工坊星图契约](WORKSHOP_GRAPH.md)、[本地游戏指南](../../probably-stolen/docs/GAME_GUIDE.md)、[金属锭纯度判定](../../probably-stolen/output/cpp2il-continuous-isil/IsilDump/Assembly-CSharp/IngotPurityHelper.txt)、[牲畜血清定义](../../probably-stolen/output/cpp2il-continuous-isil/IsilDump/Assembly-CSharp/HusbandryDirectory.txt)、[原版通关与结算](../../probably-stolen/output/cpp2il-continuous-isil/IsilDump/Assembly-CSharp/PlayerStore.txt)。
