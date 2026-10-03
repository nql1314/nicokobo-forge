using HarmonyLib;
using Il2Cpp;

namespace Nicokobo.Forge;

// Intentionally dormant: this component is not wired into ForgeBootstrap.
// docs/FORGE_PROGRESS.md keeps supplementary trash cleanup disabled until it
// is validated on a disposable save. Keep the contract check and the
// fail-safe install so enabling it later stays a one-line change.
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
            var endNight = AccessTools.Method(typeof(PlayerStore),
                nameof(PlayerStore.EndNight), Type.EmptyTypes);
            var find = AccessTools.Method(typeof(PlayerStore),
                nameof(PlayerStore.FindAllItem), [typeof(bool)]);
            var parent = AccessTools.Method(typeof(GameItem),
                nameof(GameItem.GetParentWithIdentifier), [typeof(string)]);
            var destroy = AccessTools.Method(typeof(GameItemElement),
                nameof(GameItemElement.Destroy), Type.EmptyTypes);
            if (clear?.ReturnType != typeof(void) ||
                endNight?.ReturnType != typeof(void) ||
                find?.ReturnType !=
                    typeof(Il2CppSystem.Collections.Generic.List<GameItem>) ||
                parent?.ReturnType != typeof(GameInventory) ||
                destroy?.ReturnType != typeof(void))
                throw new MissingMethodException("Native trash cleanup contract mismatch");
            try
            {
                harmony.Patch(clear,
                    prefix: new HarmonyMethod(AccessTools.Method(
                        typeof(ForgeTrashCleanup), nameof(BeforeClear))),
                    postfix: new HarmonyMethod(AccessTools.Method(
                        typeof(ForgeTrashCleanup), nameof(AfterClear))));
                harmony.Patch(endNight,
                    postfix: new HarmonyMethod(AccessTools.Method(
                        typeof(ForgeTrashCleanup), nameof(AfterEndNight))));
            }
            catch
            {
                harmony.Unpatch(clear, HarmonyPatchType.All, harmony.Id);
                harmony.Unpatch(endNight, HarmonyPatchType.All, harmony.Id);
                throw;
            }
            _enabled = true;
            log("[INFO] [NicokoboForge/Trash] daily registered-item " +
                "recycling and janitorial cleanup installed");
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
            int visitedNodes = 0;
            var bags = __instance.saveBags;
            if (bags != null && bags.Pointer != IntPtr.Zero)
                foreach (var bag in bags.Values)
                    VisitItem(bag);
            VisitInventory(__instance.gridInv);
            Capture(true);
            Capture(false);

            void Capture(bool isOwned)
            {
                var all = __instance.FindAllItem(isOwned);
                if (all == null) return;
                for (int i = 0; i < all.Count; i++)
                    VisitItem(all[i]);
            }

            void VisitInventory(GameInventory? inventory)
            {
                if (inventory == null || inventory.Pointer == IntPtr.Zero ||
                    ++visitedNodes > ForgeNumbers.Inventory.MaxTrashTraversalNodes) return;
                var contents = inventory.childItems;
                if (contents == null) return;
                for (int i = 0; i < contents.Count && i < ForgeNumbers.Inventory.MaxTrashContainerItems; i++)
                    VisitItem(contents[i]);
            }

            void VisitItem(GameItem? item)
            {
                if (item == null || item.Pointer == IntPtr.Zero ||
                    !seen.Add(item.Pointer) || ++visitedNodes > ForgeNumbers.Inventory.MaxTrashTraversalNodes) return;
                if (NativeItemRegistry.IsAppliedItem(item.identifier))
                {
                    var trash = item.GetParentWithIdentifier("trashcan");
                    if (trash != null && trash.Pointer != IntPtr.Zero)
                        candidates.Add(item);
                }
                var children = item.children;
                if (children == null) return;
                for (int i = 0; i < children.Count && i < ForgeNumbers.Inventory.MaxChildrenPerItem; i++)
                    try { VisitInventory(children[i].Cast<GameInventory>()); }
                    catch (InvalidCastException) { }
            }
        }
        catch (Exception ex)
        {
            _log?.Invoke($"[ERROR] [NicokoboForge/Trash] snapshot failed; " +
                $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void AfterEndNight(PlayerStore __instance)
    {
        BeforeClear(__instance, out var candidates);
        RemoveCandidates(candidates, "daily");
    }

    private static void AfterClear(List<GameItem>? __state) =>
        RemoveCandidates(__state, "janitorial");

    private static void RemoveCandidates(List<GameItem>? candidates,
        string source)
    {
        if (!_enabled || candidates == null) return;
        int removed = 0;
        foreach (var item in candidates)
        {
            try
            {
                if (item == null || item.Pointer == IntPtr.Zero ||
                    !NativeItemRegistry.IsAppliedItem(item.identifier)) continue;
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
        if (source == "daily" || removed != 0)
            _log?.Invoke($"[INFO] [NicokoboForge/Trash] " +
                $"source={source}; candidates={candidates.Count}; " +
                $"registeredItemsRemoved={removed}");
    }

}
