using Nicokobo.Forge;

internal static class PowerConnectorIdentityChecks
{
    internal static void Run()
    {
        var wire = new object();
        const string id = "nicokobo.logistics_nexus.wire";
        var registry = new Dictionary<string, object>(StringComparer.Ordinal) { [id] = wire };
        int checks = 0;
        void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
        Check(PowerConnectorIdentity.Find(registry, null) == null,
            "Power initialization before native identifier assignment must continue the ordinary path");
        Check(PowerConnectorIdentity.Find(registry, "") == null,
            "Empty identifiers must not reach dictionary lookup or become connectors");
        Check(PowerConnectorIdentity.Find(registry, "energy_credit_ext") == null,
            "An identified ordinary battery must retain native power initialization and charging");
        Check(ReferenceEquals(PowerConnectorIdentity.Find(registry, id), wire),
            "A registered wire must still resolve its exact provider for charging rejection and power planning");
        registry.Remove(id);
        Check(PowerConnectorIdentity.Find(registry, id) == null,
            "An unregistered ID must stop matching a stale external provider");
        Console.WriteLine($"Power connector factory identity checks: {checks} assertions passed (production lookup, offline).");
    }
}
