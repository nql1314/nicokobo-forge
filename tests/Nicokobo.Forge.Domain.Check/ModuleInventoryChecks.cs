using Il2Cpp;
using Nicokobo.Forge.Runtime;

internal static class ModuleInventoryChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Expect(bool value, string message)
        { checks++; if (!value) throw new Exception("Module inventory: " + message); }

        var machine = new GameItem();
        Expect(NativeModuleInventory.Read(machine) == null, "construction before content window is ready");
        machine.contentWindow = new PixelWindow();
        Expect(NativeModuleInventory.Read(machine) == null, "window before layout is ready");
        machine.contentWindow.childElement = new PixelElement();
        Expect(NativeModuleInventory.Read(machine) == null, "non-machine content has no module inventory");
        var layout = new GridPixelElement();
        machine.contentWindow.childElement = layout;
        Expect(NativeModuleInventory.Read(machine) == null && layout.Reads == 0, "empty grid is not queried");
        layout.gridWidth = 1; layout.gridHeight = 2;
        Expect(NativeModuleInventory.Read(machine) == null && layout.Reads == 0, "narrow grid is not queried");
        layout.gridWidth = 2; layout.gridHeight = 1;
        Expect(NativeModuleInventory.Read(machine) == null && layout.Reads == 0, "short grid is not queried");
        layout.gridHeight = 2;
        Expect(NativeModuleInventory.Read(machine) == null, "empty module position is allowed");
        layout.Elements[(1, 1)] = new PixelElement();
        Expect(NativeModuleInventory.Read(machine) == null, "other element type is not a module bay");
        var modules = new GameGridInventory();
        layout.Elements[(0, 1)] = new GameGridInventory();
        layout.Elements[(1, 1)] = modules;
        Expect(ReferenceEquals(NativeModuleInventory.Read(machine), modules), "ready lookup uses native module coordinates");
        layout.Elements.Remove((1, 1));
        Expect(NativeModuleInventory.Read(machine) == null, "removed module bay is not cached");
        layout.Elements[(1, 1)] = modules;
        modules.Pointer = IntPtr.Zero;
        Expect(NativeModuleInventory.Read(machine) == null, "invalid module pointer is rejected");
        modules.Pointer = new IntPtr(1);
        layout.Pointer = IntPtr.Zero;
        Expect(NativeModuleInventory.Read(machine) == null, "invalid layout pointer is rejected");
        layout.Pointer = new IntPtr(1);
        machine.contentWindow.Pointer = IntPtr.Zero;
        Expect(NativeModuleInventory.Read(machine) == null, "invalid window pointer is rejected");
        machine.contentWindow = new PixelWindow { childElement = new GridPixelElement() };
        Expect(NativeModuleInventory.Read(machine) == null, "replacement window cannot return previous module bay");
        Console.WriteLine($"Module inventory readiness checks passed: {checks}.");
    }
}
