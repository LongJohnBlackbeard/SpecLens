using JdeClient.Core.Exceptions;
using JdeClient.Core.Internal;
using JdeClient.Core.Models;
using static JdeClient.Core.Interop.JdeStructures;

namespace JdeClient.Core.UnitTests.Internal;

public class JdeSessionTests
{
    [Test]
    public async Task Constructor_NullOptions_Throws()
    {
        var exception = await Assert.That(() => new JdeSession(null!))
            .ThrowsExactly<ArgumentNullException>();

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.ParamName).IsEqualTo("options");
    }

    [Test]
    public async Task IsConnected_NotConnected_ReturnsFalse()
    {
        var session = new JdeSession(new JdeClientOptions());

        await Assert.That(session.IsConnected).IsFalse();
    }

    [Test]
    public async Task ConnectAsync_ConnectsAndExposesHandles()
    {
        var fakeEngine = new FakeQueryEngine
        {
            UserHandle = new HUSER { Handle = new IntPtr(111) },
            EnvironmentHandle = new HENV { Handle = new IntPtr(222) }
        };
        var session = new JdeSession(new JdeClientOptions(), _ => fakeEngine);

        try
        {
            await session.ConnectAsync();

            await Assert.That(session.IsConnected).IsTrue();
            await Assert.That(session.QueryEngine).IsSameReferenceAs(fakeEngine);
            await Assert.That(session.UserHandle.Handle).IsEqualTo(new IntPtr(111));
            await Assert.That(session.EnvironmentHandle.Handle).IsEqualTo(new IntPtr(222));
        }
        finally
        {
            await session.DisconnectAsync();
        }

        await Assert.That(fakeEngine.DisposeCount).IsEqualTo(1);
    }

    [Test]
    public async Task ConnectAsync_AlreadyConnected_Throws()
    {
        var session = new JdeSession(new JdeClientOptions(), _ => new FakeQueryEngine());
        try
        {
            await session.ConnectAsync();

            await Assert.That(async () => await session.ConnectAsync())
                .ThrowsExactly<JdeConnectionException>();
        }
        finally
        {
            await session.DisconnectAsync();
        }
    }

    [Test]
    public async Task ConnectAsync_CanceledToken_Throws()
    {
        var session = new JdeSession(new JdeClientOptions(), _ => new FakeQueryEngine());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.That(async () => await session.ConnectAsync(cancellationToken: cts.Token))
            .ThrowsExactly<OperationCanceledException>();
    }

    [Test]
    public async Task ConnectAsync_WhenInitializeThrows_WrapsConnectionException()
    {
        var fakeEngine = new FakeQueryEngine
        {
            InitializeException = new InvalidOperationException("init failed")
        };
        var session = new JdeSession(new JdeClientOptions(), _ => fakeEngine);

        var exception = await Assert.That(async () => await session.ConnectAsync())
            .ThrowsExactly<JdeConnectionException>();

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.InnerException).IsTypeOf<InvalidOperationException>();
        await Assert.That(session.IsConnected).IsFalse();
    }

    [Test]
    public async Task ExecuteAsync_Func_RunsOnWorkerAndReturnsResult()
    {
        var session = new JdeSession(new JdeClientOptions(), _ => new FakeQueryEngine());
        try
        {
            await session.ConnectAsync();

            var workerName = await session.ExecuteAsync(() => Thread.CurrentThread.Name ?? string.Empty);

            await Assert.That(workerName).IsEqualTo("JdeWorker");
        }
        finally
        {
            await session.DisconnectAsync();
        }
    }

    [Test]
    public async Task ExecuteAsync_Action_RunsOnWorker()
    {
        var session = new JdeSession(new JdeClientOptions(), _ => new FakeQueryEngine());
        try
        {
            await session.ConnectAsync();
            int value = 0;

            await session.ExecuteAsync(() => value = 42);

            await Assert.That(value).IsEqualTo(42);
        }
        finally
        {
            await session.DisconnectAsync();
        }
    }

    [Test]
    public async Task ExecuteAsync_ActionThrows_PropagatesException()
    {
        var session = new JdeSession(new JdeClientOptions(), _ => new FakeQueryEngine());
        try
        {
            await session.ConnectAsync();

            await Assert.That(async () => await session.ExecuteAsync(() => throw new InvalidOperationException("boom")))
                .ThrowsExactly<InvalidOperationException>();
        }
        finally
        {
            await session.DisconnectAsync();
        }
    }

    [Test]
    public async Task ExecuteAsync_WhenQueueClosed_ThrowsConnectionException()
    {
        var session = new JdeSession(new JdeClientOptions(), _ => new FakeQueryEngine());
        try
        {
            await session.ConnectAsync();

            var queueField = typeof(JdeSession).GetField("_workQueue", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var queue = queueField?.GetValue(session);
            await Assert.That(queue).IsNotNull();
            queue!.GetType().GetMethod("CompleteAdding")!.Invoke(queue, null);

            await Assert.That(async () => await session.ExecuteAsync(() => 1))
                .ThrowsExactly<JdeConnectionException>();
        }
        finally
        {
            await session.DisconnectAsync();
        }
    }

    [Test]
    public async Task IsConnected_Disposed_ReturnsFalse()
    {
        var session = new JdeSession(new JdeClientOptions());
        session.Dispose();

        await Assert.That(session.IsConnected).IsFalse();
    }

    [Test]
    public async Task QueryEngine_NotConnected_Throws()
    {
        var session = new JdeSession(new JdeClientOptions());

        await Assert.That(() => session.QueryEngine).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task Handles_NotConnected_Throws()
    {
        var session = new JdeSession(new JdeClientOptions());

        await Assert.That(() => session.UserHandle).ThrowsExactly<InvalidOperationException>();
        await Assert.That(() => session.EnvironmentHandle).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task EnsureConnected_NotConnected_Throws()
    {
        var session = new JdeSession(new JdeClientOptions());

        await Assert.That(() => session.EnsureConnected()).ThrowsExactly<JdeConnectionException>();
    }

    [Test]
    public async Task DisconnectAsync_NotConnected_Completes()
    {
        var session = new JdeSession(new JdeClientOptions());

        var task = session.DisconnectAsync();

        await Assert.That(task.IsCompletedSuccessfully).IsTrue();
    }

    [Test]
    public async Task Dispose_DoubleDispose_DoesNotThrow()
    {
        var session = new JdeSession(new JdeClientOptions());

        session.Dispose();
        session.Dispose();

        await Assert.That(session.IsConnected).IsFalse();
    }

    [Test]
    public async Task LogInteropSizes_DebugDisabled_LogsNothing()
    {
        var messages = new List<string>();
        var options = new JdeClientOptions
        {
            EnableDebug = false,
            LogSink = message => messages.Add(message)
        };

        JdeSession.LogInteropSizes(options);

        await Assert.That(messages.Count).IsEqualTo(0);
    }

    [Test]
    public async Task LogInteropSizes_DebugEnabled_LogsStructSizes()
    {
        var messages = new List<string>();
        var options = new JdeClientOptions
        {
            EnableDebug = true,
            LogSink = message => messages.Add(message)
        };

        JdeSession.LogInteropSizes(options);

        await Assert.That(messages.Count).IsEqualTo(6);
        await Assert.That(messages[0]).Contains("Interop sizes", StringComparison.Ordinal);
        await Assert.That(messages[1]).Contains("NID", StringComparison.Ordinal);
        await Assert.That(messages[5]).Contains("SORTSTRUCT", StringComparison.Ordinal);
    }

    private sealed class FakeQueryEngine : IF9860QueryEngine
    {
        public Exception? InitializeException { get; set; }
        public int DisposeCount { get; private set; }
        public HUSER UserHandle { get; set; }
        public HENV EnvironmentHandle { get; set; }

        public void Initialize()
        {
            if (InitializeException != null)
            {
                throw InitializeException;
            }
        }

        public List<JdeObjectInfo> QueryObjects(
            JdeObjectType? objectType = null,
            string? namePattern = null,
            string? descriptionPattern = null,
            int maxResults = 0,
            string? dataSourceOverride = null,
            bool allowDataSourceFallback = true)
        {
            return [];
        }

        public JdeObjectInfo? GetObjectByName(
            string objectName,
            JdeObjectType objectType,
            string? dataSourceOverride = null,
            bool allowDataSourceFallback = true)
        {
            return null;
        }

        public void Dispose()
        {
            DisposeCount++;
        }
    }
}
