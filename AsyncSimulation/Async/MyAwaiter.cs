using System.Runtime.CompilerServices;

namespace AsyncSimulation.Async;

public readonly struct MyAwaiter : ICriticalNotifyCompletion
{
    private readonly MyTask _task;
    public MyAwaiter(MyTask task) { _task = task; }

    public bool IsCompleted => _task.IsCompleted;

    public void GetResult()
    {
        if (_task.Exception is { } ex) throw ex;
    }

    public void OnCompleted(Action continuation) => Hook(continuation);
    public void UnsafeOnCompleted(Action continuation) => Hook(continuation);

    private void Hook(Action continuation)
    {
        var ctx = MySynchronizationContext.CurrentCustom;
        if (ctx is null) { _task.RegisterContinuation(continuation); return; }
        _task.RegisterContinuation(() => ctx.Post(_ => continuation(), null));
    }
}

public readonly struct MyAwaiter<T> : ICriticalNotifyCompletion
{
    private readonly MyTask<T> _task;
    public MyAwaiter(MyTask<T> task) { _task = task; }

    public bool IsCompleted => _task.IsCompleted;

    public T GetResult()
    {
        if (_task.Exception is { } ex) throw ex;
        return _task.Result;
    }

    public void OnCompleted(Action continuation) => Hook(continuation);
    public void UnsafeOnCompleted(Action continuation) => Hook(continuation);

    private void Hook(Action continuation)
    {
        var ctx = MySynchronizationContext.CurrentCustom;
        if (ctx is null) { _task.RegisterContinuation(continuation); return; }
        _task.RegisterContinuation(() => ctx.Post(_ => continuation(), null));
    }
}
