using Android.App;
using Android.OS;
using Android.Runtime;

namespace Xkb.Tests;

/// <summary>
/// Entry point for <c>xharness android test --instrumentation xkbcommon.tests.TestInstrumentation</c>.
/// XHarness reads the run's outcome from the <c>return-code</c> key of the result bundle.
/// </summary>
[Instrumentation(Name = "xkbcommon.tests.TestInstrumentation")]
public sealed class TestInstrumentation : Instrumentation
{
    private const string Tag = "xkbcommon-tests";

    public TestInstrumentation(IntPtr handle, JniHandleOwnership transfer)
        : base(handle, transfer)
    {
    }

    public override void OnCreate(Bundle? arguments)
    {
        base.OnCreate(arguments);
        Start();
    }

    public override void OnStart()
    {
        base.OnStart();
        int rc;
        try
        {
            rc = TestRunner.Run([]);
        }
        catch (Exception e)
        {
            Android.Util.Log.Error(Tag, e.ToString());
            rc = 1;
        }

        Android.Util.Log.Info(Tag, $"Test run finished with exit code {rc}");
        var results = new Bundle();
        results.PutInt("return-code", rc);
        Finish(rc == 0 ? Result.Ok : Result.Canceled, results);
    }
}
