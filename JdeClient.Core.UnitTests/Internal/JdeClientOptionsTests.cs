using JdeClient.Core;

namespace JdeClient.Core.UnitTests.Internal;

public class JdeClientOptionsTests
{
    [Test]
    public async Task WriteLog_WithLogSink_InvokesSink()
    {
        var messages = new List<string>();
        var options = new JdeClientOptions { LogSink = msg => messages.Add(msg) };

        options.WriteLog("test message");

        await Assert.That(messages.Count).IsEqualTo(1);
        await Assert.That(messages[0]).IsEqualTo("test message");
    }

    [Test]
    public async Task WriteLog_WithNullLogSink_FallsBackToConsole()
    {
        var options = new JdeClientOptions { LogSink = null };

        // Should not throw - falls back to Console.WriteLine
        options.WriteLog("console message");

        await Assert.That(true).IsTrue(); // No exception means success
    }

    [Test]
    public async Task WriteLog_WithThrowingLogSink_SwallowsException()
    {
        var options = new JdeClientOptions
        {
            LogSink = _ => throw new InvalidOperationException("sink error")
        };

        // Should not throw - catch block swallows exceptions
        options.WriteLog("should not throw");

        await Assert.That(true).IsTrue(); // No exception means success
    }
}
