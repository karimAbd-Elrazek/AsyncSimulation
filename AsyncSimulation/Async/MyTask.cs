using AsyncSimulation.Diagnostics;

namespace AsyncSimulation.Async;

public class MyTask
{
    private readonly object _lock = new();
    private readonly List<Action> _continuations = new();
    private bool _completed;
    private Exception? _exception;

    /// <summary>Heap id for the visualizer (assigned when the task object is allocated).</summary>
    public int HeapId { get; set; }

    public bool IsCompleted { get { lock (_lock) return _completed; } }
    public Exception? Exception { get { lock (_lock) return _exception; } }

    public void SetResult() => Complete(null);
    public void SetException(Exception ex) => Complete(ex);

    private void Complete(Exception? ex)
    {
        Action[] toFire;
        lock (_lock)
        {
            _completed = true;
            _exception = ex;
            toFire = _continuations.ToArray();
            _continuations.Clear();
        }
        Scene.Set(HeapId, "_completed", "true");
        Scene.Set(HeapId, "_continuations", "[]");
        foreach (var c in toFire) c();
    }

    internal void RegisterContinuation(Action continuation, int continuationHeapId = 0)
    {
        bool runNow;
        int count;
        lock (_lock)
        {
            runNow = _completed;
            if (!runNow) _continuations.Add(continuation);
            count = _continuations.Count;
        }
        if (runNow) { continuation(); return; }
        if (HeapId > 0)
        {
            var label = continuationHeapId > 0 ? $"[Action#{continuationHeapId}]" : $"[{count} item(s)]";
            Scene.Set(HeapId, "_continuations", label);
        }
    }

    public MyAwaiter GetAwaiter() => new(this);
}

public class MyTask<T> : MyTask
{
    private T? _result;

    public T Result
    {
        get
        {
            if (Exception is { } ex) throw ex;
            return _result!;
        }
    }

    public void SetResult(T result)
    {
        _result = result;
        Scene.Set(HeapId, "_result", result is null ? "null" : result.ToString() ?? "");
        SetResult();
    }

    public new MyAwaiter<T> GetAwaiter() => new(this);
}
