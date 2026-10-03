using System.Globalization;
using Il2Cpp;

namespace Nicokobo.Forge;

/// <summary>Persistent, pre-quality production basis. Factory default values
/// are supplied by the content owner and never used as a production floor.</summary>
public static class ForgeProductionValueApi
{
    public const string BasisTag = "NICOKOBO_FORGE_PRODUCTION_BASIS_V1";

    public static decimal InputValue(ForgeMachineBatchContext context,
        Func<ForgeMachineItemTake, decimal>? itemValue = null,
        Func<ForgeMachineLiquidTake, decimal>? liquidValue = null) =>
        checked(context.Items.Sum(take => itemValue?.Invoke(take) ?? take.Value) +
            context.Liquids.Sum(take => liquidValue?.Invoke(take) ?? take.Value));

    public static long Calculate(ForgeMachineBatchContext context, int markupPercent,
        Func<ForgeMachineItemTake, decimal>? itemValue = null,
        Func<ForgeMachineLiquidTake, decimal>? liquidValue = null) =>
        ForgeProductionValueMath.PerOutput(InputValue(context, itemValue, liquidValue), markupPercent, context.OutputCount);

    public static void SetBasis(GameItem product, long value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
        var tags = product.state?.dict ?? throw new InvalidOperationException("Product state unavailable");
        if (!tags.TryGetValue(BasisTag, out var tag) || tag == null)
            tags[BasisTag] = tag = new TagState(BasisTag, BasisTag);
        tag.Enable();
        tag.SetString(value.ToString(CultureInfo.InvariantCulture));
        // Native reads clone modifiedState, not the writable base state.
        product.SyncModifiedState();
        product.unitValue = value;
        if (ReadBasis(product, -1) != value || product.unitValue != value)
            throw new InvalidOperationException("Production basis readback mismatch");
    }

    public static long ReadBasis(GameItem product, long factoryDefault)
    {
        string? saved = product.GetTagReadonly(BasisTag)?.valueString;
        if (string.IsNullOrEmpty(saved)) return factoryDefault;
        return long.TryParse(saved, NumberStyles.None, CultureInfo.InvariantCulture, out long value) && value >= 0
            ? value : throw new InvalidOperationException("Invalid saved production basis");
    }
}
