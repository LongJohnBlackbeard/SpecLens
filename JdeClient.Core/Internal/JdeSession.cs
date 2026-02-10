using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Collections.Concurrent;
using JdeClient.Core.Exceptions;
using JdeClient.Core;
using JdeClient.Core.Interop;
using static JdeClient.Core.Interop.JdeStructures;

namespace JdeClient.Core.Internal;

/// <summary>
/// Manages JDE session - tracks connection state and query engine
/// Uses F9860 table query approach
/// </summary>
internal class JdeSession : IJdeSession
{
    private IF9860QueryEngine? _queryEngine;
    private readonly JdeClientOptions _options;
    private readonly Func<JdeClientOptions, IF9860QueryEngine> _queryEngineFactory;
    private bool _isConnected;
    private bool _disposed;
    private BlockingCollection<IWorkItem>? _workQueue;
    private Thread? _workerThread;

    /// <summary>
    /// Whether the session is connected
    /// </summary>
    public bool IsConnected => _isConnected && !_disposed;

    /// <summary>
    /// Gets the query engine (for internal use)
    /// </summary>
    /// <summary>
    /// Gets the query engine when connected.
    /// </summary>
    public IF9860QueryEngine QueryEngine => _queryEngine ?? throw new InvalidOperationException("Not connected");

    /// <summary>
    /// Gets the JDE user handle when connected.
    /// </summary>
    public HUSER UserHandle => QueryEngine.UserHandle;

    /// <summary>
    /// Gets the JDE environment handle when connected.
    /// </summary>
    public HENV EnvironmentHandle => QueryEngine.EnvironmentHandle;

    /// <summary>
    /// Connect to JDE by initializing F9860 query engine
    /// Requires JDE Fat Client (activConsole.exe) to be running
    /// </summary>
    /// <param name="specPath">Reserved for future use (ignored)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <exception cref="JdeConnectionException">Thrown if connection fails</exception>
    public JdeSession(JdeClientOptions options)
        : this(options, static opt => new F9860QueryEngine(opt))
    {
    }

    internal JdeSession(JdeClientOptions options, Func<JdeClientOptions, IF9860QueryEngine> queryEngineFactory)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _queryEngineFactory = queryEngineFactory ?? throw new ArgumentNullException(nameof(queryEngineFactory));
    }

    public Task ConnectAsync(string? specPath = null, CancellationToken cancellationToken = default)
    {
        if (_isConnected)
        {
            throw new JdeConnectionException("Already connected to JDE");
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            StartWorkerThread();
            _isConnected = true;
        }
        catch (Exception ex) when (ex is not JdeConnectionException)
        {
            StopWorkerThread();
            throw new JdeConnectionException("Failed to connect to JDE. Ensure activConsole.exe is running.", ex);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Disconnect from JDE (cleanup)
    /// </summary>
    public Task DisconnectAsync()
    {
        if (!_isConnected)
        {
            return Task.CompletedTask;
        }

        StopWorkerThread();
        _isConnected = false;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Ensure the session is connected
    /// </summary>
    /// <exception cref="JdeConnectionException">Thrown if not connected</exception>
    public void EnsureConnected()
    {
        if (!IsConnected)
        {
            throw new JdeConnectionException("Not connected to JDE. Call ConnectAsync first.");
        }
    }

    public Task<T> ExecuteAsync<T>(Func<T> action, CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        cancellationToken.ThrowIfCancellationRequested();

        var queue = _workQueue ?? throw new JdeConnectionException("JDE worker thread not initialized.");
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var workItem = new WorkItem<T>(action, tcs);

        try
        {
            queue.Add(workItem, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            throw new JdeConnectionException("JDE worker queue is closed.", ex);
        }

        return tcs.Task;
    }

    public Task ExecuteAsync(Action action, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(() =>
        {
            action();
            return true;
        }, cancellationToken);
    }

    private void StartWorkerThread()
    {
        _workQueue = new BlockingCollection<IWorkItem>();
        var readyEvent = new ManualResetEventSlim(false);
        ExceptionDispatchInfo? initException = null;
        const int stackSizeBytes = 8 * 1024 * 1024;

        _workerThread = new Thread(() =>
        {
            try
            {
                _queryEngine = _queryEngineFactory(_options);
                LogInteropSizes(_options);
                _queryEngine.Initialize();
            }
            catch (Exception ex)
            {
                _queryEngine?.Dispose();
                _queryEngine = null;
                initException = ExceptionDispatchInfo.Capture(ex);
            }
            finally
            {
                readyEvent.Set();
            }

            if (initException != null)
            {
                return;
            }

            if (_workQueue == null)
            {
                return;
            }

            foreach (var workItem in _workQueue.GetConsumingEnumerable())
            {
                try
                {
                    workItem.Execute();
                }
                catch
                {
                    // Work item should capture exceptions; keep worker alive.
                }
            }

            _queryEngine?.Dispose();
            _queryEngine = null;
        }, stackSizeBytes)
        {
            IsBackground = true,
            Name = "JdeWorker"
        };

        _workerThread.SetApartmentState(ApartmentState.MTA);
        _workerThread.Start();

        readyEvent.Wait();
        initException?.Throw();
    }

    private void StopWorkerThread()
    {
        var queue = _workQueue;
        var worker = _workerThread;

        _workQueue = null;
        _workerThread = null;

        if (queue == null || worker == null)
        {
            _queryEngine?.Dispose();
            _queryEngine = null;
            return;
        }

        if (Thread.CurrentThread == worker)
        {
            _queryEngine?.Dispose();
            _queryEngine = null;
            queue.CompleteAdding();
            return;
        }

        try
        {
            queue.CompleteAdding();
            worker.Join();
        }
        finally
        {
            queue.Dispose();
        }
    }

    private interface IWorkItem
    {
        void Execute();
    }

    private sealed class WorkItem<T> : IWorkItem
    {
        private readonly Func<T> _action;
        private readonly TaskCompletionSource<T> _tcs;

        public WorkItem(Func<T> action, TaskCompletionSource<T> tcs)
        {
            _action = action;
            _tcs = tcs;
        }

        public void Execute()
        {
            try
            {
                _tcs.TrySetResult(_action());
            }
            catch (Exception ex)
            {
                _tcs.TrySetException(ex);
            }
        }
    }

    internal static void LogInteropSizes(JdeClientOptions options)
    {
        if (!options.EnableDebug)
        {
            return;
        }

        options.WriteLog("[DEBUG] Interop sizes:");
        options.WriteLog($"[DEBUG]   NID: {Marshal.SizeOf<NID>()} bytes");
        options.WriteLog($"[DEBUG]   DBREF: {Marshal.SizeOf<DBREF>()} bytes");
        options.WriteLog($"[DEBUG]   SELECTSTRUCT: {Marshal.SizeOf<SELECTSTRUCT>()} bytes");
        options.WriteLog($"[DEBUG]   NEWSELECTSTRUCT: {Marshal.SizeOf<NEWSELECTSTRUCT>()} bytes");
        options.WriteLog($"[DEBUG]   SORTSTRUCT: {Marshal.SizeOf<SORTSTRUCT>()} bytes");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            DisconnectAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // Suppress exceptions during disposal
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
