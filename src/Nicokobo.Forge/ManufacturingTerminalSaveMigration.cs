using Il2Cpp;

namespace Nicokobo.Forge;

// Decode the same native graph through Forge's factory. No inventory links,
// UUIDs, item shapes, values, tags or child positions are replaced.
internal static class ManufacturingTerminalSaveMigration
{
    internal static int Apply(Il2CppSystem.Collections.Generic.List<SaveItemNode>? nodes)
    {
        if (nodes == null) return 0;
        int changed = 0;
        for (int index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];
            if (node == null) continue;
            string id = ForgeManufacturingTerminal.NormalizeItemId(node.identifier);
            if (id != ForgeManufacturingTerminal.ItemId) continue;
            bool migrated = node.identifier != id;
            node.identifier = id;
            if (node.spriteAtlasPath != ForgeManufacturingTerminal.AtlasKey ||
                node.spritePath != ForgeManufacturingTerminal.SpriteKey)
            {
                node.spriteAtlasPath = ForgeManufacturingTerminal.AtlasKey;
                node.spritePath = ForgeManufacturingTerminal.SpriteKey;
                migrated = true;
            }
            if (node.itemTypes == null)
                node.itemTypes = new Il2CppSystem.Collections.Generic.List<string>();
            if (!node.itemTypes.Contains("MACHINE"))
            {
                node.itemTypes.Add("MACHINE");
                migrated = true;
            }
            if (migrated) changed++;
        }
        return changed;
    }
}
