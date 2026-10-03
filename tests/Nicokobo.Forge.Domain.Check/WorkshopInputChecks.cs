using Nicokobo.Forge.Workshop;

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

        var capture = new WorkshopInputCapture();
        capture.Close();
        capture.Update(0, true);
        Expect(!capture.BlocksInput, "A closed workshop captured ordinary game input");

        capture.Open();
        Expect(capture.BlocksInput, "Opening the workshop left the first click unprotected");
        capture.Update(1, false);
        capture.Update(2, true);
        Expect(capture.BlocksInput, "Visible workshop let empty-space or held input through");

        capture.Close();
        Expect(capture.BlocksInput, "Closing the workshop exposed its mouse-down frame");
        for (int frame = 3; frame <= 5; frame++) capture.Update(frame, true);
        Expect(capture.BlocksInput, "Held closing click reached the background");
        capture.Update(6, false);
        capture.Update(6, false);
        Expect(capture.BlocksInput, "Closing mouse-up reached a later callback in the release frame");
        capture.Close();
        capture.Update(7, false);
        Expect(!capture.BlocksInput, "Repeated close prevented ordinary input from resuming");

        capture.Open();
        capture.Close();
        capture.Update(8, false);
        capture.Update(9, true);
        Expect(capture.BlocksInput, "A new press during close drained into the game");
        capture.Update(10, false);
        Expect(capture.BlocksInput, "A second release was not protected");
        capture.Update(11, false);
        Expect(!capture.BlocksInput, "Input stayed blocked after the second release frame");

        capture.Open();
        capture.Close();
        capture.Update(12, false);
        capture.Open();
        capture.Update(13, false);
        Expect(capture.BlocksInput, "Reopening inherited a pending close release");
        capture.Close();
        capture.Update(14, false);
        Expect(capture.BlocksInput, "Keyboard or programmatic close leaked same-frame input");
        capture.Update(15, false);
        Expect(!capture.BlocksInput, "Keyboard close never returned input to the game");
        Console.WriteLine($"Workshop input capture: {checks} assertions passed (modal, close, release, reopen).");
    }
}
