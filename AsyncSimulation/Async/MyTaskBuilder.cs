using System.Runtime.CompilerServices;
using AsyncSimulation.Diagnostics;

namespace AsyncSimulation.Async;

public struct MyTaskBuilder
{
    private MyTask _task;
    private IMyStateMachine? _box;
    private int _boxHeapId;

    public int BoxHeapId => _boxHeapId;

    public static MyTaskBuilder Create()
    {
        var task = new MyTask();
        task.HeapId = Scene.Alloc("MyTask",
            ("_completed", "false"),
            ("_continuations", "[]"));
        return new MyTaskBuilder { _task = task };
    }

    public MyTask Task => _task;

    public void Start<TStateMachine>(ref TStateMachine sm) where TStateMachine : IMyStateMachine
        => sm.MoveNext();

    public void SetStateMachine(IMyStateMachine sm) => _box = sm;

    public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine sm)
        where TAwaiter : INotifyCompletion
        where TStateMachine : IMyStateMachine
    {
        EnsureBoxed(ref sm);
        awaiter.OnCompleted(_box!.MoveNext);
    }

    public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine sm)
        where TAwaiter : ICriticalNotifyCompletion
        where TStateMachine : IMyStateMachine
    {
        EnsureBoxed(ref sm);
        Scene.Alloc("Action",
            ("Target", $"→ Boxed SM #{_boxHeapId}"),
            ("Method", "MoveNext"));
        awaiter.UnsafeOnCompleted(_box!.MoveNext);
    }

    private void EnsureBoxed<TStateMachine>(ref TStateMachine sm) where TStateMachine : IMyStateMachine
    {
        if (_box is not null) return;
        // Ask the SM for its current field values so the heap card matches reality
        // (in particular: __state has already been mutated by the caller before we get here).
        var fields = sm.SnapshotHeapFields().ToArray();
        _boxHeapId = Scene.Alloc($"Boxed {typeof(TStateMachine).Name}", fields);
        IMyStateMachine boxed = sm;
        boxed.SetStateMachine(boxed);
        _box = boxed;
    }

    public void SetResult() => _task.SetResult();
    public void SetException(Exception ex) => _task.SetException(ex);
}

public struct MyTaskBuilder<T>
{
    private MyTask<T> _task;
    private IMyStateMachine? _box;
    private int _boxHeapId;

    public int BoxHeapId => _boxHeapId;

    public static MyTaskBuilder<T> Create()
    {
        var task = new MyTask<T>();
        task.HeapId = Scene.Alloc($"MyTask<{typeof(T).Name}>",
            ("_completed", "false"),
            ("_continuations", "[]"),
            ("_result", "null"));
        return new MyTaskBuilder<T> { _task = task };
    }

    public MyTask<T> Task => _task;

    public void Start<TStateMachine>(ref TStateMachine sm) where TStateMachine : IMyStateMachine
        => sm.MoveNext();

    public void SetStateMachine(IMyStateMachine sm) => _box = sm;

    public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine sm)
        where TAwaiter : INotifyCompletion
        where TStateMachine : IMyStateMachine
    {
        EnsureBoxed(ref sm);
        awaiter.OnCompleted(_box!.MoveNext);
    }

    public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine sm)
        where TAwaiter : ICriticalNotifyCompletion
        where TStateMachine : IMyStateMachine
    {
        EnsureBoxed(ref sm);
        Scene.Alloc("Action",
            ("Target", $"→ Boxed SM #{_boxHeapId}"),
            ("Method", "MoveNext"));
        awaiter.UnsafeOnCompleted(_box!.MoveNext);
    }

    private void EnsureBoxed<TStateMachine>(ref TStateMachine sm) where TStateMachine : IMyStateMachine
    {
        if (_box is not null) return;
        var fields = sm.SnapshotHeapFields().ToArray();
        _boxHeapId = Scene.Alloc($"Boxed {typeof(TStateMachine).Name}", fields);
        IMyStateMachine boxed = sm;
        boxed.SetStateMachine(boxed);
        _box = boxed;
    }

    public void SetResult(T result) => _task.SetResult(result);
    public void SetException(Exception ex) => _task.SetException(ex);
}
