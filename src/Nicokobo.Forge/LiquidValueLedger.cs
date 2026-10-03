using System.Text.Json;

namespace Nicokobo.Forge;

internal static class LiquidValueLedger
{
    internal static string Encode(ForgeMachineLiquidSnapshot snapshot) => JsonSerializer.Serialize(
        snapshot.Contents.Where(part => part.Value != null && part.Parts > 0).ToArray());

    internal static ForgeMachineLiquidSnapshot Decode(string saved, ForgeMachineLiquidSnapshot current)
    {
        if (string.IsNullOrEmpty(saved)) return current;
        var entries = JsonSerializer.Deserialize<ForgeMachineLiquidPart[]>(saved)
            ?? throw new InvalidDataException("Liquid value ledger is null");
        if (entries.Any(part => part == null || part.Parts <= 0 || part.Value is null or < 0 || part.QualityBasis < 0 ||
                !current.Contents.Any(now => now.LiquidId == part.LiquidId)) ||
            entries.Select(part => part.LiquidId).Distinct(StringComparer.Ordinal).Count() != entries.Length)
            throw new InvalidDataException("Invalid liquid value ledger");
        return Reconcile(current with { Contents = current.Contents.Select(part =>
            entries.SingleOrDefault(entry => entry.LiquidId == part.LiquidId) ?? part).ToArray() }, current);
    }

    internal static ForgeMachineLiquidSnapshot Reconcile(ForgeMachineLiquidSnapshot before,
        ForgeMachineLiquidSnapshot current) => current with
    {
        Contents = Array.AsReadOnly(current.Contents.Select(part =>
        {
            var previous = before.Contents.SingleOrDefault(entry => entry.LiquidId == part.LiquidId);
            if (previous?.Value == null || part.Parts == 0) return part with { Value = null, QualityBasis = null };
            decimal value = part.Parts <= previous.Parts
                ? previous.Parts == 0 ? 0 : ForgeProductionValueMath.RemainingLiquidValue(
                    previous.Value.Value, previous.Parts, part.Parts)
                : checked(previous.Value.Value + ForgeProductionValueMath.NativeLiquidValue(part.Parts - previous.Parts));
            decimal? basis = previous.QualityBasis == null ? null : part.Parts <= previous.Parts
                ? ForgeProductionValueMath.RemainingLiquidValue(previous.QualityBasis.Value, previous.Parts, part.Parts)
                : checked(previous.QualityBasis.Value + ForgeProductionValueMath.NativeLiquidValue(part.Parts - previous.Parts));
            return part with { Value = value, QualityBasis = basis };
        }).ToArray())
    };
}
