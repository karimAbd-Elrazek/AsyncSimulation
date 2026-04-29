using System.Collections.Concurrent;

namespace AsyncSimulation.Async;

public static class MyThreadPool
{
    private static readonly BlockingCollection<(Action Work, string Label)> Queue = new();
    private static bool _initialized;

    public static void Initialize(int workerCount = 3)
    {
        if (_initialized) return;
        _initialized = true;
        for (int i = 0; i < workerCount; i++)
        {
            new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = $"Worker-{i}"
            }.Start();
        }
    }

    private static void WorkerLoop()
    {
        foreach (var (work, _) in Queue.GetConsumingEnumerable())
            work();
    }

    public static void QueueWorkItem(Action work, string label) => Queue.Add((work, label));

    public static void Shutdown() => Queue.CompleteAdding();
}
