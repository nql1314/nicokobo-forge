using Nicokobo.Forge;

internal static class PowerBatchChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool pass, string reason) { checks++; if (!pass) throw new Exception(reason); }
        var power = ForgePowerBatchMath.Plan([new(1, 80), new(2, 35), new(3, 10)], 90);
        Check(power != null && power.SequenceEqual(new[] { new ForgePowerSourceDebit(1, 80, 80), new(2, 35, 10) }),
            "90 energy must leave 0/25/10 from absolute amounts 80/35/10");
        Check(ForgePowerBatchMath.Plan([new(1, 80), new(2, 35)], 116) == null, "Insufficient total must not produce a debit prefix");
        Check(ForgePowerBatchMath.Plan([new(1, 10), new(1, 20)], 5) == null, "Duplicate native instance ID must reject the plan");
        Check(ForgePowerBatchMath.Plan([new(0, 10)], 5) == null, "Unstable native instance ID must reject the plan");
        Check(ForgePowerBatchMath.Plan([new(1, -1)], 0) == null, "Invalid energy cannot enter an empty-cost plan");
        Check(ForgePowerBatchMath.Plan([new(4, 35), new(2, 35)], 40)?.SequenceEqual(new[] { new ForgePowerSourceDebit(2, 35, 35), new(4, 35, 5) }) == true,
            "Equal absolute energy must use stable instance ID order");
        Check(ForgePowerBatchMath.Plan([new(1, int.MaxValue), new(2, int.MaxValue)], int.MaxValue)?.Count == 1,
            "Total energy sum must not overflow 32 bits");
        Check(ForgePowerBatchMath.Plan([new(1, 10)], 0)?.Count == 0, "Zero cost needs no debit receipt");
        int battery = 80, material = 5, output = 0, quota = 0;
        var result = ForgeTransactionApi.Run([
            new("energy", () => battery -= 70, () => { battery = 80; return true; }),
            new("materials", () => material = 0, () => { material = 5; return true; }),
            new("output", () => output = 1, () => { output = 0; return true; }),
            new("quota", () => { quota = 1; throw new Exception("injected after write"); }, () => { quota = 0; return true; })]);
        Check(!result.Committed && result.Restored && battery == 80 && material == 5 && output == 0 && quota == 0,
            "Content commit failure must compensate output, materials and energy including the throwing write");
        var trace = new List<string>();
        result = ForgeTransactionApi.Run([
            new("first", () => { }, () => { trace.Add("first"); return true; }),
            new("second", () => throw new Exception("apply"), () => { trace.Add("second"); throw new Exception("restore"); })]);
        Check(!result.Committed && !result.Restored && trace.SequenceEqual(new[] { "second", "first" }),
            "Uncertain compensation must still attempt preceding resources");
        Console.WriteLine($"Shared batch transactions and external power math: {checks} assertions passed (offline).");
    }
}
