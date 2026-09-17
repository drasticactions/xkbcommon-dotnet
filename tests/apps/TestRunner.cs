using System.Reflection;
using Xunit;

namespace Xkb.Tests;

/// <summary>
/// A small reflection runner over the shared test suite for the device and browser host
/// apps. xunit's in-process runner cannot be used there: it subscribes to
/// <c>Console.CancelKeyPress</c>, which throws on iOS and tvOS.
/// </summary>
internal static class TestRunner
{
    private const string DynamicSkipToken = "$XunitDynamicSkip$";

    public static int Run(string[] args)
    {
        var passed = 0;
        var failed = 0;
        var skipped = 0;
        var filterText = args.Length > 0 ? args[0] : Environment.GetEnvironmentVariable("XKB_TEST_FILTER");
        var filters = string.IsNullOrWhiteSpace(filterText) ? [] : filterText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var tests = typeof(TestRunner).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttribute<FactAttribute>() is not null && m.GetParameters().Length == 0)
                .Select(m => (Type: t, Method: m)))
            .Where(x => filters.Length == 0 || filters.Any(f => $"{x.Type.FullName}.{x.Method.Name}".Contains(f, StringComparison.Ordinal)))
            .OrderBy(x => x.Type.FullName).ThenBy(x => x.Method.Name)
            .ToList();

        foreach (var (type, method) in tests)
        {
            var name = $"{type.Name}.{method.Name}";
            var fact = method.GetCustomAttribute<FactAttribute>()!;
            if (fact.Skip is not null)
            {
                skipped++;
                Console.WriteLine($"  SKIP {name}: {fact.Skip}");
                continue;
            }

            object? instance = null;
            try
            {
                instance = Activator.CreateInstance(type);
                var result = method.Invoke(instance, null);
                if (result is Task task)
                {
                    task.GetAwaiter().GetResult();
                }
                else if (result is ValueTask valueTask)
                {
                    valueTask.GetAwaiter().GetResult();
                }

                passed++;
                Console.WriteLine($"  PASS {name}");
            }
            catch (Exception e)
            {
                var inner = e is TargetInvocationException { InnerException: { } tie } ? tie : e;
                if (inner.Message.StartsWith(DynamicSkipToken, StringComparison.Ordinal))
                {
                    skipped++;
                    Console.WriteLine($"  SKIP {name}: {inner.Message[DynamicSkipToken.Length..]}");
                }
                else
                {
                    failed++;
                    Console.WriteLine($"  FAIL {name}");
                    Console.WriteLine(inner);
                }
            }
            finally
            {
                (instance as IDisposable)?.Dispose();
            }
        }

        Console.WriteLine($"Total: {tests.Count}, Passed: {passed}, Failed: {failed}, Skipped: {skipped}");
        return failed == 0 && tests.Count > 0 ? 0 : 1;
    }
}
