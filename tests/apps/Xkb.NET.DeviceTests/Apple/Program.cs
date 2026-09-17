namespace Xkb.Tests;

internal static class Program
{
    // No UIApplication: the process runs the suite and exits, and XHarness
    // reads the exit code (--expected-exit-code 0).
    private static int Main(string[] args)
    {
        var rc = TestRunner.Run(args);
        Console.WriteLine($"Test run finished with exit code {rc}");
        return rc;
    }
}
