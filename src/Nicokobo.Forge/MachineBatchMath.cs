namespace Nicokobo.Forge;

public static class ForgeMachinePowerMath
{
    public static int CalculateCost(int cost, int outputCount, bool perOutput = false)
    {
        if (cost < 0 || outputCount is < 1 or > 256) throw new ArgumentOutOfRangeException(nameof(cost));
        return checked(cost * (perOutput ? outputCount : 1));
    }
}

internal static class MachineBatchMath
{
    internal static ForgeMachineLiquidSnapshot Consume(ForgeMachineLiquidSnapshot before, int parts, string? liquidId)
    {
        if (parts <= 0 || before.Contents.Any(part => part.Parts < 0) || parts > before.TotalParts)
            throw new ArgumentOutOfRangeException(nameof(parts));
        var amounts = before.Contents.Select(part => part.Parts).ToArray();
        if (liquidId != null)
        {
            int index = before.Contents.ToList().FindIndex(part => part.LiquidId == liquidId);
            if (index < 0 || amounts[index] < parts) throw new ArgumentOutOfRangeException(nameof(liquidId));
            amounts[index] -= parts;
        }
        else
        {
            // Exact proportional draw. Largest fractional remainders receive
            // the leftover parts, so rounding never creates or loses volume.
            var draws = amounts.Select(amount => (int)((long)amount * parts / before.TotalParts)).ToArray();
            int leftover = parts - draws.Sum();
            foreach (int index in Enumerable.Range(0, amounts.Length).OrderByDescending(index =>
                (long)amounts[index] * parts % before.TotalParts).ThenBy(index => index).Take(leftover)) draws[index]++;
            for (int index = 0; index < amounts.Length; index++) amounts[index] -= draws[index];
        }
        return before with { Contents = Array.AsReadOnly(before.Contents.Select((part, index) =>
            part with { Parts = amounts[index] }).ToArray()) };
    }

    internal static ForgeMachineLiquidSnapshot Add(ForgeMachineLiquidSnapshot before,
        IReadOnlyList<ForgeMachineLiquidPart> contents)
    {
        var amounts = before.Contents.ToDictionary(part => part.LiquidId, part => part.Parts, StringComparer.Ordinal);
        foreach (var part in contents)
        {
            if (part.Parts <= 0 || !amounts.ContainsKey(part.LiquidId))
                throw new ArgumentOutOfRangeException(nameof(contents));
            amounts[part.LiquidId] = checked(amounts[part.LiquidId] + part.Parts);
        }
        var result = before with { Contents = Array.AsReadOnly(before.Contents.Select(part =>
            part with { Parts = amounts[part.LiquidId] }).ToArray()) };
        if (result.TotalParts > result.CapacityParts) throw new ArgumentOutOfRangeException(nameof(contents));
        return result;
    }
}
