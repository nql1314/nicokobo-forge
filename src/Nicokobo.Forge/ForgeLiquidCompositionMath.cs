namespace Nicokobo.Forge;

/// <summary>Read-only component arithmetic for content-owned conversions.
/// Volumes use native parts; these helpers never change a live container.</summary>
public static class ForgeLiquidCompositionMath
{
    public static IReadOnlyList<ForgeMachineLiquidPart> Consumed(
        ForgeMachineLiquidSnapshot before, ForgeMachineLiquidSnapshot after)
    {
        if (before.CapacityParts != after.CapacityParts || before.Contents.Count != after.Contents.Count)
            throw new ArgumentException("Liquid snapshots do not describe the same container");
        var drawn = new List<ForgeMachineLiquidPart>();
        for (int index = 0; index < before.Contents.Count; index++)
        {
            var from = before.Contents[index]; var remaining = after.Contents[index];
            int parts = checked(from.Parts - remaining.Parts);
            if (from.LiquidId != remaining.LiquidId || parts < 0 || remaining.Parts < 0 ||
                from.Value < 0 || remaining.Value < 0 || from.QualityBasis < 0 || remaining.QualityBasis < 0)
                throw new ArgumentException("Liquid component changed unexpectedly");
            if (parts == 0) continue;
            decimal? value = from.Value == null ? null : from.Value - (remaining.Value ?? 0);
            decimal? basis = from.QualityBasis == null ? null : from.QualityBasis - (remaining.QualityBasis ?? 0);
            if (value < 0 || basis < 0) throw new ArgumentException("Liquid component value increased during a draw");
            drawn.Add(new(from.LiquidId, parts) { Value = value, QualityBasis = basis });
        }
        return Array.AsReadOnly(drawn.ToArray());
    }

    /// <summary>Replace just one component. All other parts and values remain
    /// unchanged; an existing target component is combined without value loss.</summary>
    public static IReadOnlyList<ForgeMachineLiquidPart> ReplaceComponent(
        IReadOnlyList<ForgeMachineLiquidPart> contents, string sourceId, string targetId)
    {
        if (string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(targetId) || sourceId == targetId ||
            contents.Any(part => part.Parts <= 0 || part.Value < 0 || part.QualityBasis < 0 ||
                part.QualityBasis != null && part.Value == null) ||
            contents.Select(part => part.LiquidId).Distinct(StringComparer.Ordinal).Count() != contents.Count)
            throw new ArgumentException("Invalid component conversion");
        var converted = contents.Where(part => part.LiquidId == sourceId || part.LiquidId == targetId).ToArray();
        if (converted.Length == 0) return Array.AsReadOnly(contents.ToArray());
        var result = contents.Where(part => part.LiquidId != sourceId && part.LiquidId != targetId).ToList();
        result.Add(new(targetId, checked(converted.Sum(part => part.Parts)))
        {
            Value = converted.Any(part => part.Value != null) ? BaseValue(converted) : null,
            QualityBasis = converted.Any(part => part.QualityBasis != null)
                ? converted.Sum(part => part.QualityBasis ?? part.Value ?? ForgeProductionValueMath.NativeLiquidValue(part.Parts)) : null
        });
        return Array.AsReadOnly(result.ToArray());
    }

    public static decimal BaseValue(IReadOnlyList<ForgeMachineLiquidPart> contents) =>
        contents.Sum(part => part.Value ?? ForgeProductionValueMath.NativeLiquidValue(part.Parts));
}
