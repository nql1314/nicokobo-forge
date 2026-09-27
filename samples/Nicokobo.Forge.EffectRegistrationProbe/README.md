# Nicokobo Forge 效果注册测试 Mod

独立 MelonLoader 测试插件，依赖 `Nicokobo.Forge.dll`。只注册
`nicokobo.forge.effect_probe.effect.ping`，不生成物品、不加入掉落或商店，也不写存档。
`randomEligible=false` 要求 Nicokobo Forge 在原生随机效果抽取时排除它。

启动游戏并进入主菜单后，检查 `MelonLoader/Latest.log`：

1. `[EffectProbe] submit=Accepted` 仅表示 Nicokobo Forge 接受声明。
2. `[NicokoboForge/Effect] ... status=Applied` 与
   `[EffectProbe] status=Applied; nativeRegistryPresent=True; capability=True`
   才表明原生效果表接纳了该效果。
3. `[EffectProbe] callback=onAny` 仅在游戏实际对带此效果的物品计算时出现。
   本测试插件不会自动创建或发放这种物品，因此前两项不证明回调已运行。

若 30 秒后仍是 `Staged`，或出现 `Failed`、`Conflict`、
`nativeRegistryPresent=False`，保留本次日志用于诊断。测试完成后可从
`Mods` 移除 `Nicokobo.Forge.EffectRegistrationProbe.dll`；没有需要迁移的存档数据。

2026-09-26 的首次运行已验证注册与原生表接纳，详见
[运行记录](../../docs/probes/2026-09-26-effect-registration-probe.md)。
`callbackCount=0`，所以尚未验证实际效果执行或随机池排除行为。
