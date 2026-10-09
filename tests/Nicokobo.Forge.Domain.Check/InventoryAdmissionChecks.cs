using Il2Cpp;
using Nicokobo.Forge;
using Nicokobo.Forge.Runtime;

// Exercise the production registration and installed postfix. Native results
// are supplied as an I/O boundary; this does not simulate IL2CPP DynamicInvoke.
internal static class InventoryAdmissionChecks
{
    internal static void Run()
    {
        int checks = 0, calls = 0;
        void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
        bool Throws(Action action)
        { try { action(); return false; } catch (ArgumentException) { return true; } }
        const string ownerId = "test.admission", rootId = ownerId + ".region", inventoryId = "contents";
        var root = new GameItem { identifier = rootId, Owned = true };
        var grid = new GameGridInventory { identifier = inventoryId, AdmissionParent = root };
        var battery = new GameItem { identifier = "energy_credit_ext", Owned = true };
        bool Admit(GameGridInventory inventory, GameItem item, bool nativeResult = true)
        {
            object[] args = [inventory, item, nativeResult];
            NativeHookSet.Postfix(typeof(GameGridInventory), nameof(GameGridInventory.MayHaveValidInventorySlot), args);
            return (bool)args[2];
        }
        NativeHookSet.InstallResult = false;
        ForgeInventoryAdmissionApi.Install(true, _ => { });
        Check(!ForgeInventoryAdmissionApi.IsAvailable, "A missing admission hook must disable registration");
        bool unavailable = false;
        try { ForgeInventoryAdmissionApi.Register(ownerId, rootId, inventoryId, (_, _) => true); }
        catch (InvalidOperationException) { unavailable = true; }
        Check(unavailable, "Content must not enable its grid without the required hook");
        NativeHookSet.InstallResult = true;
        ForgeInventoryAdmissionApi.Install(true, _ => throw new Exception("log failure"));
        Check(Throws(() => ForgeInventoryAdmissionApi.Register("other.owner", rootId, inventoryId, (_, _) => true)),
            "A different owner must not replace another item's admission");
        using var lease = ForgeInventoryAdmissionApi.Register(ownerId, rootId, inventoryId, (item, _) =>
        { calls++; if (item.identifier == "throw") throw new Exception("predicate failure"); return item.Owned && item.identifier == "energy_credit_ext"; });
        Check(Throws(() => ForgeInventoryAdmissionApi.Register(ownerId, rootId, inventoryId, (_, _) => true)),
            "A second registration cannot replace the frozen restriction");
        Check(Admit(grid, battery), "A native-accepted battery must survive the managed restriction");
        Check(!Admit(grid, new GameItem { identifier = "common_ore", Owned = true }), "Rejected contents must remain outside");
        Check(!Admit(grid, new GameItem { identifier = "energy_credit_ext" }), "An unowned candidate must be rejected");
        int before = calls;
        Check(!Admit(grid, battery, false) && calls == before,
            "The postfix must preserve native false without invoking content or bypassing locks, ownership, cycles or shape");
        root.Owned = false;
        Check(!Admit(grid, battery) && calls == before, "An unowned root cannot execute its content predicate");
        root.Owned = true;
        grid.identifier = "other-inventory";
        Check(Admit(grid, battery) && calls == before, "Another inventory under this root keeps its original admission");
        grid.identifier = inventoryId;
        grid.AdmissionParent = new GameItem { identifier = "dossier", Owned = true };
        Check(Admit(grid, new GameItem { identifier = "document", Owned = true }) && calls == before,
            "Ordinary dossier admission is unaffected by a registered region");
        grid.AdmissionParent = new GameItem { identifier = "", Owned = true };
        Check(Admit(grid, battery) && calls == before, "An unidentified ordinary root must retain native behavior");
        grid.AdmissionParent = root;
        Check(!Admit(grid, new GameItem { identifier = "throw", Owned = true }),
            "A content exception must fail closed even if logging also throws");
        lease.Dispose();
        before = calls;
        Check(Admit(grid, battery) && calls == before, "Disposing the lease must release the old managed callback");
        Console.WriteLine($"Inventory admission checks: {checks} assertions passed (production postfix, offline native boundary).");
    }
}

namespace Il2Cpp
{
    public sealed partial class GameGridInventory
    {
        public string identifier = "";
        public GameItem? AdmissionParent;
        public GameItem? GetParentItem() => AdmissionParent;
        public bool MayHaveValidInventorySlot(GameItem item) => true;
    }
}
