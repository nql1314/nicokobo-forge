using MelonLoader;
using Nicokobo.Forge;
using Nicokobo.Forge.Registration;

[assembly: MelonInfo(typeof(Nicokobo.Forge.ExampleTwo.Plugin), "Nicokobo Forge Example Two", "0.1.0", "Nicokobo")]
[assembly: MelonProcess("Probably Stolen.exe")]

namespace Nicokobo.Forge.ExampleTwo;

public sealed class Plugin : MelonMod
{
    public override void OnInitializeMelon()
    {
        var result = ForgeApi.Register("nicokobo.forge.example_two", "0.1.0", batch =>
        {
            batch.Require("nicokobo.forge.example_one");
            batch.Define(DefinitionKind.Item, "dependent", "template=node_small;demo=metadata-only",
                "nicokobo.forge.example_one.item.starter");
        });
        LoggerInstance.Msg($"[Nicokobo Forge Example Two] registration={result.Status}; {result.Reason}");
    }
}
