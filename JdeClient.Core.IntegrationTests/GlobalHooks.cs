using System.Diagnostics;
using TUnit.Core.Exceptions;

namespace JdeClient.Core.IntegrationTests;

/// <summary>
/// Hooks that automatically skip tests if the JDE Fat Client (activeConsole) is not running.
/// </summary>
public static class GlobalHooks
{
    private const string RequiredProcess = "activConsole";
    private static volatile bool _ok;

    // Runs once before any tests are started
    [Before(TestSession)]
    public static void CheckOnce()
    {
        _ok = IsRunning(RequiredProcess);
    }
    
    // Runs once for every individual test
    [BeforeEvery(Test)]
    public static void EnforceForEveryTest()
    {
        if (_ok) return;

        // Skip all tests if the JDE runtime isn't available.
        throw new SkipTestException($"Required process '{RequiredProcess}' is not running.");
    }
    
    private static bool IsRunning(string processName) => Process.GetProcessesByName(processName).Length > 0;
    
}
