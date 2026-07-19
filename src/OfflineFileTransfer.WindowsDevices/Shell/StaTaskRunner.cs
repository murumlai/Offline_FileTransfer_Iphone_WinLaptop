using System.Runtime.Versioning;

namespace OfflineFileTransfer.WindowsDevices.Shell;

/// <summary>
/// Runs delegates on a dedicated single-threaded-apartment (STA) thread.
/// The Windows Shell COM automation objects require STA; this keeps provider
/// calls safe regardless of the caller's threading model.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class StaTaskRunner : IDisposable
{
    private readonly Thread _thread;
    private readonly BlockingWorkQueue _queue = new();

    public StaTaskRunner()
    {
        _thread = new Thread(Loop)
        {
            IsBackground = true,
            Name = "WPD-STA",
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public Task<T> RunAsync<T>(Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Enqueue(() =>
        {
            try
            {
                tcs.SetResult(func());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    public Task RunAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return RunAsync<bool>(() =>
        {
            action();
            return true;
        });
    }

    private void Loop()
    {
        foreach (var work in _queue.Consume())
        {
            work();
        }
    }

    public void Dispose() => _queue.CompleteAdding();

    private sealed class BlockingWorkQueue
    {
        private readonly Queue<Action> _items = new();
        private readonly object _gate = new();
        private bool _completed;

        public void Enqueue(Action work)
        {
            lock (_gate)
            {
                if (_completed)
                {
                    throw new InvalidOperationException("Runner has been disposed.");
                }

                _items.Enqueue(work);
                Monitor.Pulse(_gate);
            }
        }

        public void CompleteAdding()
        {
            lock (_gate)
            {
                _completed = true;
                Monitor.PulseAll(_gate);
            }
        }

        public IEnumerable<Action> Consume()
        {
            while (true)
            {
                Action work;
                lock (_gate)
                {
                    while (_items.Count == 0 && !_completed)
                    {
                        Monitor.Wait(_gate);
                    }

                    if (_items.Count == 0 && _completed)
                    {
                        yield break;
                    }

                    work = _items.Dequeue();
                }

                yield return work;
            }
        }
    }
}
