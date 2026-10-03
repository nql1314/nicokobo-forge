using Nicokobo.Forge;

static class WorkshopInputChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Expect(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            checks++;
        }

        var capture = new ForgeWindowInputCapture();
        capture.Close(0, false);
        Expect(!capture.BlocksPointer(0, false, false), "A closed window captured ordinary input");

        capture.Open(1, true);
        Expect(capture.BlocksPointer(1, false, true), "The activation press leaked outside the new window");
        Expect(capture.BlocksPointer(2, false, false), "The activation release reached inventory handlers");
        Expect(!capture.BlocksPointer(3, false, false), "An open window blocked controls outside its rectangle");
        Expect(capture.BlocksPointer(3, true, false), "The page body did not block underlying hover and scroll");
        Expect(capture.BlocksPointer(4, true, true), "A page press reached the inventory underneath");
        Expect(capture.BlocksPointer(5, false, true), "Dragging a page out of its rectangle leaked the held gesture");
        Expect(capture.BlocksPointer(6, false, false), "A page-owned release leaked after leaving the rectangle");
        Expect(!capture.BlocksPointer(7, false, false), "Input did not resume after the page-owned release");

        capture.Close(8, true);
        Expect(capture.BlocksPointer(8, false, true), "Closing exposed the X mouse-down frame");
        Expect(capture.BlocksPointer(9, false, true), "The held X click reached the background");
        Expect(capture.BlocksPointer(10, false, false), "The X release reached the background");
        Expect(capture.BlocksPointer(10, false, false), "A second callback in the release frame leaked input");
        capture.Close(10, false);
        Expect(!capture.BlocksPointer(11, false, false), "Repeated close kept input blocked");

        capture.Open(12, false);
        Expect(capture.BlocksPointer(12, false, false), "Programmatic opening leaked same-frame input");
        Expect(!capture.BlocksPointer(13, false, false), "Keyboard opening blocked outside controls indefinitely");
        capture.Close(14, false);
        Expect(capture.BlocksPointer(14, false, false), "Keyboard close leaked same-frame input");
        Expect(!capture.BlocksPointer(15, false, false), "Keyboard close never returned input");

        capture.Open(16, false);
        Expect(!capture.BlocksPointer(17, false, true), "An outside inventory press was swallowed");
        Expect(!capture.BlocksPointer(18, true, true), "An outside inventory drag stopped on entering the page");
        Expect(!capture.BlocksPointer(19, true, false), "The page swallowed an outside drag's mouse-up, leaving an item held");
        Expect(!capture.BlocksPointer(19, true, false), "A later release callback lost the outside gesture");
        Expect(capture.BlocksPointer(20, true, false), "Normal page shielding did not resume after the outside drag");
        capture.Close(21, false);
        Expect(!capture.BlocksPointer(22, true, false), "A closed page retained its hit rectangle");

        capture.Open(23, true);
        capture.Close(23, true);
        capture.Open(24, false);
        Expect(capture.BlocksPointer(24, false, false), "Reopening inherited a pending close");
        Expect(!capture.BlocksPointer(25, false, false), "Reopening preserved an old held mouse gesture");
        capture.Reset();
        Expect(!capture.BlocksPointer(26, true, false), "Scene reset retained a page hit area");

        capture.Open(27, false);
        Expect(!capture.BlocksPointer(28, false, true), "An outside drag was captured before keyboard close");
        capture.Close(29, true);
        Expect(!capture.BlocksPointer(29, false, true), "Keyboard close interrupted an outside drag");
        Expect(!capture.BlocksPointer(30, true, true), "The closing window captured an outside held gesture");
        Expect(!capture.BlocksPointer(31, true, false), "Keyboard close swallowed an outside drag release");
        Expect(!capture.BlocksPointer(32, true, false), "Outside release left closed-window input blocked");
        Console.WriteLine($"Window input capture: {checks} assertions passed (bounds, gesture ownership, X, release, reopen).");
    }
}
