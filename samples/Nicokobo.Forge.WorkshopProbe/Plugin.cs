using Il2Cpp;
using MelonLoader;
using Nicokobo.Forge.Registration;
using UnityEngine.Localization.Settings;

[assembly: MelonInfo(typeof(Nicokobo.Forge.WorkshopProbe.Plugin),
    "Nicokobo Forge Workshop Probe", "0.1.0", "Nicokobo")]
[assembly: MelonProcess("Probably Stolen.exe")]

namespace Nicokobo.Forge.WorkshopProbe;

/// <summary>Content example for tab switching and unlock callbacks.
/// No credits, items, game effects or save data are changed.</summary>
public sealed class Plugin : MelonMod
{
    private const string OwnerId = "nicokobo.forge.workshop_probe";
    private const string TabId = OwnerId + ".workshop";
    private static readonly string[] Ids =
    [
        OwnerId + ".signal", OwnerId + ".relay",
        OwnerId + ".beacon", OwnerId + ".link"
    ];
    private static readonly ForgeWorkshopGroup[] Groups =
    [
        new("基础测试", "Basic checks", 2),
        new("组合测试", "Combined checks", 2)
    ];
    private readonly HashSet<string> _unlocked = new(StringComparer.Ordinal);
    private string? _runId;
    private string _message = "";

    public override void OnInitializeMelon()
    {
        var result = ForgeWorkshopApi.RegisterTab(OwnerId, TabId,
            "工坊测试", "Workshop Probe", Snapshot, Unlock);
        LoggerInstance.Msg($"[WorkshopProbe] registration={result.Status}; {result.Reason}");
        if (result.Status is not (SubmitStatus.Accepted or SubmitStatus.AlreadyPresent))
            LoggerInstance.Warning("[WorkshopProbe] tab was not registered");
    }

    private ForgeWorkshopSnapshot? Snapshot()
    {
        var store = CurrentStore();
        if (store == null) return null;
        if (_runId != store.runID)
        {
            _runId = store.runID;
            _unlocked.Clear();
            _message = "";
        }
        bool english = PreferEnglish();
        ForgeWorkshopEntry[] entries =
        [
            Entry(0, "测试信号", "Test Signal", "验证直接解锁。",
                "Check a direct unlock.", null, english),
            Entry(1, "中继器", "Relay", "需要先解锁测试信号。",
                "Requires Test Signal.", 0, english),
            Entry(2, "信标", "Beacon", "可独立选择与解锁。",
                "Can be selected and unlocked independently.", null, english),
            Entry(3, "组合链路", "Combined Link", "需要中继器和信标。",
                "Requires Relay and Beacon.", 1, english,
                extraPrerequisite: 2)
        ];
        return new ForgeWorkshopSnapshot(store.playerCash, english,
            Groups, entries, _message);
    }

    private ForgeWorkshopEntry Entry(int index, string zh, string en,
        string descriptionZh, string descriptionEn, int? prerequisite,
        bool english, int? extraPrerequisite = null)
    {
        bool done = _unlocked.Contains(Ids[index]);
        bool ready = (!prerequisite.HasValue ||
                _unlocked.Contains(Ids[prerequisite.Value])) &&
            (!extraPrerequisite.HasValue ||
                _unlocked.Contains(Ids[extraPrerequisite.Value]));
        string requirement = prerequisite switch
        {
            null => english ? "Prerequisite: none" : "前置：无",
            0 => english ? "Prerequisite: Test Signal" : "前置：测试信号",
            _ => english ? "Prerequisite: Relay + Beacon" : "前置：中继器 + 信标"
        };
        return new ForgeWorkshopEntry(Ids[index], english ? en : zh,
            english ? "Workshop API test" : "工坊接口测试",
            english ? descriptionEn : descriptionZh, requirement,
            english ? "Test cost: none\nItems: none" :
                "测试费用：无\n所需物品：无",
            english ? "Free" : "免费",
            done ? (english ? "Unlocked for this session" : "本次测试已解锁")
                : !ready ? (english ? "Prerequisite missing" : "缺少前置")
                : (english ? "Ready" : "可解锁"),
            done ? (english ? "Unlocked" : "已解锁")
                : (english ? "Unlock" : "解锁"),
            done, ready, !done && ready);
    }

    private void Unlock(string entryId)
    {
        if (CurrentStore() == null || Array.IndexOf(Ids, entryId) < 0) return;
        // Recheck the current snapshot; Forge's displayed state can be stale.
        var entry = Snapshot()?.Entries.FirstOrDefault(x => x.Id == entryId);
        if (entry?.ActionEnabled != true) return;
        _unlocked.Add(entryId);
        _message = PreferEnglish() ? "Test unlock recorded for this session." :
            "测试解锁已记录在本次运行中。";
        LoggerInstance.Msg($"[WorkshopProbe] unlocked={entryId}; run={_runId}");
    }

    private static PlayerStore? CurrentStore()
    {
        try
        {
            var store = PlayerStore.Instance;
            return store != null && store.Pointer != IntPtr.Zero &&
                !string.IsNullOrWhiteSpace(store.runID)
                ? store : null;
        }
        catch { return null; }
    }

    private static bool PreferEnglish()
    {
        try
        {
            var code = LocalizationSettings.SelectedLocale?.Identifier.Code;
            return code?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch { return false; }
    }
}
