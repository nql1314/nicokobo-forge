using System.Collections;
using System.Reflection;
using System.Text.Json;

namespace Nicokobo.CompatibilityPatches.Patches.PCExpansionScalars;

// Only field-name aliases are normalized. Values and snapshot payloads are retained.
internal static class ScalarAliases
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public;
    private static readonly MethodInfo Copy = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly string[] Members =
        ["Scalars", "State", "ModifiedState", "Features", "Fake", "Real", "Effects", "Tags", "Nodes", "SlotItems", "Composition"];

    internal static string Name(string name)
    {
        const string il2cpp = "_k__BackingField", managed = ">k__BackingField";
        if (name.StartsWith('_') && name.EndsWith(il2cpp, StringComparison.Ordinal) && name.Length > il2cpp.Length + 1)
            return name[1..^il2cpp.Length];
        if (name.StartsWith('<') && name.EndsWith(managed, StringComparison.Ordinal) && name.Length > managed.Length + 1)
            return name[1..^managed.Length];
        return name;
    }

    internal static Dictionary<string, JsonElement> RestoreFields(Type type, Dictionary<string, JsonElement> fields)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            string name = Name(field.Key);
            var property = Property(type, field.Key) ?? Property(type, name) ??
                Property(type, "_" + name + "_k__BackingField") ?? Property(type, "<" + name + ">k__BackingField");
            if (property == null)
                throw new InvalidOperationException("物品字段不再支持：" + field.Key);
            Add(result, property.Name, field.Value);
        }
        return result;
    }

    private static PropertyInfo? Property(Type type, string name)
    {
        var property = type.GetProperty(name, Instance);
        if (property?.GetMethod?.IsPublic != true || property.SetMethod?.IsPublic != true ||
            property.GetIndexParameters().Length != 0) return null;
        Type value = property.PropertyType;
        return value == typeof(string) || value == typeof(bool) || value == typeof(int) || value == typeof(long) ||
            value == typeof(float) || value == typeof(double) || value.IsEnum ? property : null;
    }

    private static Dictionary<string, JsonElement> Canonical(Dictionary<string, JsonElement> fields)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var field in fields) Add(result, Name(field.Key), field.Value);
        return result;
    }

    private static void Add(Dictionary<string, JsonElement> fields, string name, JsonElement value)
    {
        if (fields.TryGetValue(name, out var previous))
        {
            if (!Same(previous, value)) throw new InvalidOperationException("物品字段别名值冲突：" + name);
            return;
        }
        fields.Add(name, value);
    }

    private static bool Same(JsonElement a, JsonElement b) => a.ValueKind == b.ValueKind && a.ValueKind switch
    {
        JsonValueKind.Number => a.TryGetDecimal(out var left) && b.TryGetDecimal(out var right) ? left == right : a.GetDouble().Equals(b.GetDouble()),
        JsonValueKind.String => a.GetString() == b.GetString(),
        JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null => true,
        _ => a.GetRawText() == b.GetRawText()
    };

    internal static object ComparisonCopy(object value)
    {
        object copy = Copy.Invoke(value, null)!;
        foreach (string name in Members)
        {
            var property = value.GetType().GetProperty(name, Instance);
            var field = property == null ? value.GetType().GetField(name, Instance) : null;
            if (property == null && field == null) continue;
            object? original = property != null ? property.GetValue(value) : field!.GetValue(value);
            if (original == null) continue;
            object replacement;
            if (original is Dictionary<string, JsonElement> scalars) replacement = Canonical(scalars);
            else if (original is Dictionary<string, Dictionary<string, JsonElement>> tags)
                replacement = tags.ToDictionary(pair => pair.Key, pair => Canonical(pair.Value), StringComparer.Ordinal);
            else if (original is IList list)
            {
                var items = (IList)Activator.CreateInstance(list.GetType())!;
                foreach (object? item in list) items.Add(item == null ? null : ComparisonCopy(item));
                replacement = items;
            }
            else throw new InvalidOperationException("装机快照字段结构不受支持：" + name);
            if (property != null) property.SetValue(copy, replacement);
            else field!.SetValue(copy, replacement);
        }
        return copy;
    }
}

