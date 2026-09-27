using MelonLoader;
using Nicokobo.Forge;
using Nicokobo.Forge.Registration;

[assembly: MelonInfo(typeof(Nicokobo.Forge.ExampleOne.Plugin), "Nicokobo Forge Example One", "0.1.0", "Nicokobo")]
[assembly: MelonProcess("Probably Stolen.exe")]

namespace Nicokobo.Forge.ExampleOne;

public sealed class Plugin : MelonMod
{
    public override void OnInitializeMelon()
    {
        var result = ForgeApi.Register("nicokobo.forge.example_one", "0.1.0", batch =>
        {
            batch.Define(DefinitionKind.Item, "starter", "template=node_small;demo=metadata-only");
            batch.Define(DefinitionKind.Effect, "boost", "demo=metadata-only",
                "nicokobo.forge.example_one.item.starter");
        });
        LoggerInstance.Msg($"[Nicokobo Forge Example One] registration={result.Status}; {result.Reason}");
    }
}
