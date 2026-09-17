namespace Xkb.Tests;

internal static class Program
{
    private static int Main(string[] args)
    {
        var rc = TestRunner.Run(args);
        // XHarness ends the browser run when it sees this line.
        Console.WriteLine($"WASM EXIT {rc}");
        return rc;
    }
}
