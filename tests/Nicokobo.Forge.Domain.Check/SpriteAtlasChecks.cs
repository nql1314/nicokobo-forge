using Nicokobo.Forge;
using Nicokobo.Forge.Runtime;

internal static class SpriteAtlasChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool condition, string message)
        { checks++; if (!condition) throw new Exception(message); }
        void Throws(Action action, string message)
        { bool threw = false; try { action(); } catch { threw = true; } Check(threw, message); }
        ForgeSpriteDefinition[] definitions = [new("one", "one.png", 16, 32), new("two", "two.png", 16, 16)];
        var port = new AtlasFixture();
        var result = SpriteAtlasInstaller.Install(definitions, port);
        Check(result.Installed && ReferenceEquals(result.Retained, port.Current) && port.Loaded == 2 && port.Released == 0,
            "Atlas was not completely loaded and verified before success");
        var foreign = port.Current;
        result = SpriteAtlasInstaller.Install(definitions, port);
        Check(!result.Installed && ReferenceEquals(port.Current, foreign) && port.Created == 1,
            "Existing atlas was replaced");
        foreach (string fault in new[] { "load", "publish-before", "publish-after", "verify" })
        {
            port = new AtlasFixture { Fault = fault };
            result = SpriteAtlasInstaller.Install(definitions, port);
            Check(!result.Installed && result.Retained == null && port.Current == null && port.Released == 1,
                "Failed atlas leaked a published entry or resources: " + fault);
        }
        port = new AtlasFixture { Fault = "verify", RemoveFault = true };
        result = SpriteAtlasInstaller.Install(definitions, port);
        Check(!result.Installed && ReferenceEquals(port.Current, result.Retained) && port.Released == 0,
            "Failed removal destroyed a live cache's resources");
        port = new AtlasFixture { Fault = "verify", IgnoreRemoval = true };
        result = SpriteAtlasInstaller.Install(definitions, port);
        Check(!result.Installed && result.Retained != null && port.Released == 0,
            "Removal without effect was treated as success");
        port = new AtlasFixture { Fault = "verify", ReplaceDuringVerify = true };
        result = SpriteAtlasInstaller.Install(definitions, port);
        Check(!result.Installed && port.Current != null && port.Removed == 0 && port.Released == 1,
            "Rollback removed another publisher's replacement");
        port = new AtlasFixture { ClaimDuringLoad = true };
        result = SpriteAtlasInstaller.Install(definitions, port);
        Check(!result.Installed && port.Current != null && port.Published == 0 && port.Released == 1,
            "Late cache conflict was overwritten");
        port = new AtlasFixture { Fault = "load", ReleaseFault = true };
        result = SpriteAtlasInstaller.Install(definitions, port);
        Check(result.Retained != null && result.Error is AggregateException,
            "Cleanup failure lost the retained resources or original error");

        var assembly = typeof(SpriteAtlasChecks).Assembly;
        var atlas = ForgeAssetsApi.CreateEmbeddedSpriteAtlas("test.assets", "test/atlas", assembly, definitions);
        definitions[0] = new("mutated", "bad.png", 99, 99);
        var original = new[] { new ForgeSpriteDefinition("one", "one.png", 16, 32), definitions[1] };
        Check(ReferenceEquals(atlas, ForgeAssetsApi.CreateEmbeddedSpriteAtlas("test.assets", "test/atlas", assembly, original)),
            "Declaration was not frozen or identical registration was not reused");
        Throws(() => ForgeAssetsApi.CreateEmbeddedSpriteAtlas("test.foreign", "test/atlas", assembly, original),
            "Foreign owner acquired a claimed atlas key");
        Throws(() => ForgeAssetsApi.CreateEmbeddedSpriteAtlas("test.assets", "test/atlas", assembly, original, ForgeSpriteFilter.Bilinear),
            "Different filter replaced an atlas registration");
        Throws(() => ForgeAssetsApi.CreateEmbeddedSpriteAtlas("test.assets", "test/duplicate", assembly, [original[0], original[0]]),
            "Duplicate sprite keys accepted");
        Throws(() => ForgeAssetsApi.CreateEmbeddedSpriteAtlas("bad", "test/invalid", assembly, original), "Invalid owner accepted");
        var item = new Il2Cpp.GameItem();
        Throws(() => atlas.Assign(item, "one"), "Uninstalled sprite assigned as rendered");
        atlas.Assign(item, "one", allowStaging: true);
        Check(item.spriteAtlasPath == "test/atlas" && item.spritePath == "one" && item.SpriteAssignments == 0,
            "Staged icon invoked native sprite assignment");
        Throws(() => atlas.Assign(item, "unknown", allowStaging: true), "Unknown sprite staged");
        port = new AtlasFixture(); NativeSpriteAtlasPort.Next = port;
        Check(atlas.TryInstall(() => throw new Exception("logger")) && atlas.Installed && !atlas.Failed && atlas.Count == 2,
            "Throwing notification changed installed state");
        Check(atlas.TryInstall(() => throw new Exception("must not notify again")) && port.Created == 1,
            "Repeated installation decoded the atlas again");
        atlas.Assign(item, "one");
        Check(item.SpriteAssignments == 1, "Installed sprite was not assigned");
        var failed = ForgeAssetsApi.CreateEmbeddedSpriteAtlas("test.assets", "test/failed", assembly, original);
        NativeSpriteAtlasPort.Next = new AtlasFixture { Fault = "verify" };
        int failures = 0;
        Check(!failed.TryInstall(onFailure: _ => { failures++; throw new Exception("logger"); }) && failed.Failed,
            "Throwing failure notification escaped");
        Check(!failed.TryInstall(onFailure: _ => failures++) && failures == 1,
            "Terminal installation retried or notified twice");
        Throws(() => failed.Assign(item, "one", allowStaging: true), "Failed atlas continued staging unusable paths");
        var pending = ForgeAssetsApi.CreateEmbeddedSpriteAtlas("test.assets", "test/pending", assembly, original);
        NativeSpriteAtlasPort.Next = null;
        Check(!pending.TryInstall() && !pending.Failed, "Cache-not-ready was treated as terminal failure");
        NativeSpriteAtlasPort.Next = null;
        Console.WriteLine($"Sprite atlas: {checks} assertions passed (ownership, frozen declarations, publication, rollback, notifications).");
    }
}

internal sealed class AtlasFixture : ISpriteAtlasPort<NativeSpriteAtlas>
{
    internal NativeSpriteAtlas? Current;
    internal string? Fault;
    internal bool RemoveFault, IgnoreRemoval, ReplaceDuringVerify, ClaimDuringLoad, ReleaseFault;
    internal int Created, Loaded, Published, Removed, Released;
    public bool Occupied => Current != null;
    public NativeSpriteAtlas Create() { Created++; return new(); }
    public void Load(NativeSpriteAtlas atlas, ForgeSpriteDefinition definition)
    {
        Loaded++;
        if (ClaimDuringLoad) Current = new();
        if (Fault == "load") throw new Exception("decode");
    }
    public void Publish(NativeSpriteAtlas atlas)
    {
        Published++;
        if (Fault == "publish-before") throw new Exception("before write");
        Current = atlas;
        if (Fault == "publish-after") throw new Exception("after write");
    }
    public bool IsCurrent(NativeSpriteAtlas atlas) => ReferenceEquals(Current, atlas);
    public void Verify(NativeSpriteAtlas atlas, ForgeSpriteDefinition definition)
    {
        if (ReplaceDuringVerify) Current = new();
        if (Fault == "verify") throw new Exception("readback");
    }
    public void Remove()
    {
        Removed++;
        if (RemoveFault) throw new Exception("remove");
        if (!IgnoreRemoval) Current = null;
    }
    public void Release(NativeSpriteAtlas atlas)
    {
        Released++;
        if (ReleaseFault) throw new Exception("release");
    }
}
