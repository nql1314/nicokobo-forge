# 2026-10-02 液体组分转换：本地验证

目标：Probably Stolen Demo Steam Build 25382790；Forge 0.6.4、合成扩展0.9.9、伪人0.1.13。只执行源码、逻辑、元数据、编译与本地打包检查，未安装DLL或启动游戏。

## 变更

- `ForgeMachineContainerOutput.ResolveContents` 在液体输入扣除快照确定后解析输出；与静态Contents互斥。动态输出继续走原有容量检查、原生写后读回和事务恢复。
- `ForgeLiquidCompositionMath.Consumed` 取得实际取出组分与比例价值；`ReplaceComponent` 合并被替换组分与现有目标组分，保留其它组分；所有玩法ID、纯度及价格系数由内容方持有。
- 合成扩展K04将水转换为原生protein，保留盐／化合物等组分并混入已有输出容器；保留既有质量增产与性能节水公式，两端容器不消费。C07自定义生物质罐移除。
- 内容方共同规则：生物质严格超过50%可用于合成；90%／99%／99.9%起分别加10%／25%／50%。价格账本先按实际组分计算，内容Hook替换原生水质价格修正。
- 高科技眼镜使用原生power_source_item类型判定，鉴定可从任意原生电池取电，仍每次15点。

## 本地证据

`D:\workzone\probably-stolen\mods-melonloader\Build-ForgeMods.ps1 -DefaultLogLevel INFO`完整执行：

| 检查 | 结果 |
| --- | --- |
| Forge通用原生适配元数据 | 50项通过 |
| 机器模板／冻结／追加配方 | 49项通过 |
| 液体组分／容量／失败恢复 | 125项通过 |
| 生产价值与组分账本 | 32项通过 |
| 运行边界 | 88项通过 |
| 合成扩展领域规则 | 796项通过 |
| 伪人领域规则 | 433项通过 |
| 模组矩阵 | 89项通过 |
| 物流 | L0=21、resource=37、power=27通过 |
| 合成扩展离线原生签名 | 熔炉2项，液体／电池13项通过 |
| 核心及六个示例、四个调用者 | Release编译通过 |
| 机核系列三个发布包 | Obfuscar及Harmony类型保留检查通过 |
| 非机器图标 | 29件尺寸／透明通道／图源检查通过；合成扩展实际嵌入25件图标 |

共同Forge DLL SHA256：`53A953D21C43CA75C15C87830A10012FB2221561588BE46069B10E6C12A9F56E`。完整构建日志保留在`D:\workzone\probably-stolen\_build\synthesis-item-fixes-20261002\build-forge-mods.log`。

发布DLL元数据另行读回：25件图标均在包内，精密零件资源字节与最新32×32 PNG一致，生物质六个Harmony回调名称保留，生产端绑定组分解析器，伪人消费端绑定共享阈值规则，眼镜电池发现方法调用原生电池类型判定。记录为`_build/synthesis-item-fixes-20261002/release-verification.json`。

失败恢复检查注入部分写入与读回失败，确认源、目标组分和比例价值恢复；这属于逻辑检查，不是游戏内原生事务验收。实际拖放、过夜、倾倒、成交和玩家存档往返均待另行测试。
