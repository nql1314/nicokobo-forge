using Nicokobo.Forge;

internal static class EndpointCreationChecks
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool pass) { checks++; if (!pass) throw new Exception("Endpoint creation receipt lifetime failed"); }
        var endpoint = new ForgeInventoryEndpoint(); int calls = 0;
        var borrowed = new ForgeInventoryEndpointCreation(endpoint, false, () => throw new Exception("Borrowed deletion attempted"));
        Check(ReferenceEquals(borrowed.Endpoint, endpoint) && !borrowed.Created);
        Check(borrowed.RollbackCreation() && borrowed.RollbackCreation());
        var created = new ForgeInventoryEndpointCreation(endpoint, true, () => ++calls > 1);
        Check(created.Created && !created.RollbackCreation() && calls == 1);
        Check(created.RollbackCreation() && calls == 2);
        Check(created.RollbackCreation() && calls == 2);
        bool first = true;
        var retry = new ForgeInventoryEndpointCreation(endpoint, true, () => { if (first) { first = false; throw new InvalidOperationException(); } return true; });
        try { retry.RollbackCreation(); Check(false); } catch (InvalidOperationException) { Check(true); }
        Check(retry.RollbackCreation());
        return checks;
    }
}

// The managed receipt only holds this native endpoint value. Native ownership
// guards are exercised at the content I/O boundary and require live validation.
namespace Nicokobo.Forge { public sealed record ForgeInventoryEndpoint; }
