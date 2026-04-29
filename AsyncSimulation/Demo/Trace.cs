using AsyncSimulation.Diagnostics;

namespace AsyncSimulation.Demo;

/// <summary>
/// All free-form logging for the demo. Push/Pop record stack frames; field mutations
/// (__state, __sN) are recorded directly via Scene.Set in MoveNext.
///
/// Stack-frame note: each MoveNext call gets its own frame and pops when THAT call
/// returns. RunDemo pops independently — at the end of the RunDemo method body, NOT
/// when the task finally completes (by then RunDemo has long since returned).
/// </summary>
internal static class Trace
{
    public static void DemoStarted()
    {
        Scene.Reset();
        Scene.Push("MainViewModel.RunDemo()");
        Scene.Log("Main", "Demo started");
    }

    /// <summary>
    /// Pops RunDemo at end of the method body — i.e. when the UI thread returns to
    /// the WPF command handler. The continuations registered by RunDemo fire LATER,
    /// on a fresh dispatcher pump call, not inside RunDemo.
    /// </summary>
    public static void DemoReturned()
    {
        Scene.Pop("MainViewModel.RunDemo()");
        Scene.Log("Main", "RunDemo() returned → UI thread is free");
    }

    /// <summary>Logs the final completion. Does NOT pop RunDemo — that is already gone.</summary>
    public static void DemoComplete(string word)
        => Scene.Log("Main", $"Demo complete → \"{word}\" (continuation fired on UI thread)");

    public static void EnterMoveNext(int entryState)
    {
        Scene.Push($"MoveNext (entry state {entryState})");
        Scene.Log("StateMachine",
            entryState == -1 ? "MoveNext entered (initial run)" : $"MoveNext resumed (state was {entryState})");
    }

    public static void ExitMoveNext(int entryState)
        => Scene.Pop($"MoveNext (entry state {entryState})");

    public static void Suspended(int awaitNumber)
        => Scene.Log("StateMachine", $"SUSPENDED at await #{awaitNumber} → stack about to unwind");

    public static void Resumed(int awaitNumber, LetterResult result)
        => Scene.Log("StateMachine",
            $"RESUMED after await #{awaitNumber} → got '{result.Letter}' (computed on T{result.ComputedOnThreadId})");

    public static void Concatenated(string word)
        => Scene.Log("StateMachine", $"All 6 awaits done → result = \"{word}\"");
}
