using HarmonyLib;
using Il2Cpp;

namespace Nicokobo.Forge;

internal static class ForgeTrashCleanup
{
    private static Action<string>? _log;
    private static bool _enabled;

    internal static void Install(HarmonyLib.Harmony harmony, bool knownBuild,
        Action<string> log)
    {
        _log = log;
        if (!knownBuild) return;
        try
        {
            var clear = AccessTools.Method(typeof(PlayerStore),
                nameof(PlayerStore.ClearTrash), Type.EmptyTypes);
            var find = AccessTools.Method(typeof(PlayerStore),
                nameof(PlayerStore.FindAllItem), [typeof(bool)]);
            var parent = AccessTools.Method(typeof(GameItem),
                nameof(GameItem.GetParentWithIdentifier), [typeof(string)]);
            var destroy = AccessTools.Method(typeof(GameItemElement),
                nameof(GameItemElement.Destroy), Type.EmptyTypes);
            if (clear?.ReturnType != typeof(void) ||
                find?.ReturnType !=
                    typeof(Il2CppSystem.Collections.Generic.List<GameItem>) ||
                parent?.ReturnType != typeof(GameInventory) ||
                destroy?.ReturnType != typeof(void))
                throw new MissingMethodException("Native trash cleanup contract mismatch");
            harmony.Patch(clear,
                prefix: new HarmonyMethod(AccessTools.Method(
                    typeof(ForgeTrashCleanup), nameof(BeforeClear))),
                postfix: new HarmonyMethod(AccessTools.Method(
                    typeof(ForgeTrashCleanup), nameof(AfterClear))));
            _enabled = true;
            log("[NicokoboForge/Trash] registered-item cleanup installed");
        }
        catch (Exception ex)
        {
            log($"[ERROR] [NicokoboForge/Trash] disabled; " +
                $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void BeforeClear(PlayerStore __instance,
        out List<GameItem>? __state)
    {
        __state = null;
        if (!_enabled || __instance == null || __instance.Pointer == IntPtr.Zero)
            return;
        try
        {
            var candidates = new List<GameItem>();
            __state = candidates;
            var seen = new HashSet<IntPtr>();
            Capture(true);
            Capture(false);

            void Capture(bool isOwned)
            {
                var all = __instance.FindAllItem(isOwned);
                if (all == null) return;
                for (int i = 0; i < all.Count; i++)
                {
                    var item = all[i];
                    if (item == null || item.Pointer == IntPtr.Zero ||
                        !seen.Add(item.Pointer) ||
                        !ForgeNativeApi.IsAppliedItem(item.identifier)) continue;
                    var trash = item.GetParentWithIdentifier("trashcan");
                    if (trash != null && trash.Pointer != IntPtr.Zero)
                        candidates.Add(item);
                }
            }
        }
        catch (Exception ex)
        {
            _log?.Invoke($"[ERROR] [NicokoboForge/Trash] snapshot failed; " +
                $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void AfterClear(List<GameItem>? __state)
    {
        if (!_enabled || __state == null) return;
        int removed = 0;
        foreach (var item in __state)
        {
            try
            {
                if (item == null || item.Pointer == IntPtr.Zero ||
                    !ForgeNativeApi.IsAppliedItem(item.identifier)) continue;
                var trash = item.GetParentWithIdentifier("trashcan");
                if (trash == null || trash.Pointer == IntPtr.Zero) continue;
                item.Destroy();
                removed++;
            }
            catch (Exception ex)
            {
                _log?.Invoke($"[ERROR] [NicokoboForge/Trash] item cleanup failed; " +
                    $"{ex.GetType().Name}: {ex.Message}");
            }
        }
        if (removed != 0)
            _log?.Invoke($"[NicokoboForge/Trash] remaining registered items removed={removed}");
    }
}
