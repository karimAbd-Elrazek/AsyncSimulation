using AsyncSimulation.Async;
using AsyncSimulation.Diagnostics;

namespace AsyncSimulation.Demo;

public record LetterResult(string Letter, int ComputedOnThreadId, long ElapsedMs);

public static class LetterComputation
{
    public static MyTask<LetterResult> ComputeF()    => Schedule("F");
    public static MyTask<LetterResult> ComputeI()    => Schedule("I");
    public static MyTask<LetterResult> ComputeN()    => Schedule("N");
    public static MyTask<LetterResult> ComputeA()    => Schedule("A");
    public static MyTask<LetterResult> ComputeL()    => Schedule("L");
    public static MyTask<LetterResult> ComputeBang() => Schedule("!");

    private static MyTask<LetterResult> Schedule(string letter)
    {
        var task = new MyTask<LetterResult>();
        task.HeapId = Scene.Alloc("MyTask<LetterResult>",
            ("letter", $"\"{letter}\""),
            ("_completed", "false"),
            ("_continuations", "[]"),
            ("_result", "null"));

        MyThreadPool.QueueWorkItem(() =>
        {
            var start = DateTime.Now;
            Thread.Sleep(2000);
            var elapsed = (long)(DateTime.Now - start).TotalMilliseconds;
            task.SetResult(new LetterResult(letter, Environment.CurrentManagedThreadId, elapsed));
        }, $"Compute{letter}");

        return task;
    }
}
