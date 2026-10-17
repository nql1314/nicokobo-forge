using System.Reflection;
using Il2Cpp;

namespace Nicokobo.Forge;

/// <summary>Read-only identity of the shop using the native inventor stock path.</summary>
public static class ForgeNightShopApi
{
    private static readonly Lazy<FieldInfo?> BrewingVisit = new(() =>
    {
        // Forty-Seven's shop keeps UPGRADE_MERCHANT and runs the original stock
        // method; its visit flag remains set after its stock postfix completes.
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType("XIAOWOBrewingBarrel.NightBrewingShop", false);
            if (type == null) continue;
            var field = type.GetField("_brewingVisit", BindingFlags.Static | BindingFlags.NonPublic);
            return field?.FieldType == typeof(bool) ? field : null;
        }
        return null;
    });

    /// <summary>True only at Jackson's night shop, excluding known shops sharing its scene.
    /// Query during stock generation, after content mods have loaded.</summary>
    public static bool IsInventorShop
    {
        get
        {
            var map = MapUIManager.Instance;
            return map != null && map.isOutside &&
                map.currentLocation == MapUIManager.MapLocation.UPGRADE_MERCHANT &&
                BrewingVisit.Value?.GetValue(null) is not true;
        }
    }
}
