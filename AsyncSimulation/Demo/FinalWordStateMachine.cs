using AsyncSimulation.Async;
using AsyncSimulation.Diagnostics;

namespace AsyncSimulation.Demo;

/// <summary>
/// Hand-written equivalent of what the C# compiler would generate for:
///
///     async MyTask&lt;string&gt; BuildFinalWordAsync()
///     {
///         var s1 = await ComputeF();
///         var s2 = await ComputeI();
///         var s3 = await ComputeN();
///         var s4 = await ComputeA();
///         var s5 = await ComputeL();
///         var s6 = await ComputeBang();
///         return s1 + s2 + s3 + s4 + s5 + s6;
///     }
/// </summary>
public struct FinalWordStateMachine : IMyStateMachine
{
    public int __state;
    public MyTaskBuilder<string> __builder;

    // Lifted locals (each crosses an await boundary).
    public LetterResult? __s1, __s2, __s3, __s4, __s5, __s6;

    // Awaiter slots (one per await).
    private MyAwaiter<LetterResult> __aw1, __aw2, __aw3, __aw4, __aw5, __aw6;

    public Action<LetterResult, int>? LetterCompleted;

    private void SetState(int newState)
    {
        __state = newState;
        Scene.Set(__builder.BoxHeapId, "__state", newState.ToString());
    }

    private void RecordResult(int n, LetterResult r)
        => Scene.Set(__builder.BoxHeapId, $"__s{n}", $"\"{r.Letter}\"");

    public IEnumerable<(string Name, string Value)> SnapshotHeapFields()
    {
        yield return ("__state", __state.ToString());
        yield return ("__s1", FormatLetter(__s1));
        yield return ("__s2", FormatLetter(__s2));
        yield return ("__s3", FormatLetter(__s3));
        yield return ("__s4", FormatLetter(__s4));
        yield return ("__s5", FormatLetter(__s5));
        yield return ("__s6", FormatLetter(__s6));
    }

    private static string FormatLetter(LetterResult? r) => r is null ? "null" : $"\"{r.Letter}\"";

    public void MoveNext()
    {
        // Capture entry state so push/pop names match even after SetState mutates __state.
        int entryState = __state;
        Trace.EnterMoveNext(entryState);
        try
        {
            switch (__state)
            {
                case 0: goto After1;
                case 1: goto After2;
                case 2: goto After3;
                case 3: goto After4;
                case 4: goto After5;
                case 5: goto After6;
            }

            __aw1 = LetterComputation.ComputeF().GetAwaiter();
            if (!__aw1.IsCompleted) { SetState(0); Trace.Suspended(1); __builder.AwaitUnsafeOnCompleted(ref __aw1, ref this); return; }
            After1: __s1 = __aw1.GetResult(); RecordResult(1, __s1); Trace.Resumed(1, __s1); LetterCompleted?.Invoke(__s1, 1);

            __aw2 = LetterComputation.ComputeI().GetAwaiter();
            if (!__aw2.IsCompleted) { SetState(1); Trace.Suspended(2); __builder.AwaitUnsafeOnCompleted(ref __aw2, ref this); return; }
            After2: __s2 = __aw2.GetResult(); RecordResult(2, __s2); Trace.Resumed(2, __s2); LetterCompleted?.Invoke(__s2, 2);

            __aw3 = LetterComputation.ComputeN().GetAwaiter();
            if (!__aw3.IsCompleted) { SetState(2); Trace.Suspended(3); __builder.AwaitUnsafeOnCompleted(ref __aw3, ref this); return; }
            After3: __s3 = __aw3.GetResult(); RecordResult(3, __s3); Trace.Resumed(3, __s3); LetterCompleted?.Invoke(__s3, 3);

            __aw4 = LetterComputation.ComputeA().GetAwaiter();
            if (!__aw4.IsCompleted) { SetState(3); Trace.Suspended(4); __builder.AwaitUnsafeOnCompleted(ref __aw4, ref this); return; }
            After4: __s4 = __aw4.GetResult(); RecordResult(4, __s4); Trace.Resumed(4, __s4); LetterCompleted?.Invoke(__s4, 4);

            __aw5 = LetterComputation.ComputeL().GetAwaiter();
            if (!__aw5.IsCompleted) { SetState(4); Trace.Suspended(5); __builder.AwaitUnsafeOnCompleted(ref __aw5, ref this); return; }
            After5: __s5 = __aw5.GetResult(); RecordResult(5, __s5); Trace.Resumed(5, __s5); LetterCompleted?.Invoke(__s5, 5);

            __aw6 = LetterComputation.ComputeBang().GetAwaiter();
            if (!__aw6.IsCompleted) { SetState(5); Trace.Suspended(6); __builder.AwaitUnsafeOnCompleted(ref __aw6, ref this); return; }
            After6: __s6 = __aw6.GetResult(); RecordResult(6, __s6); Trace.Resumed(6, __s6); LetterCompleted?.Invoke(__s6, 6);

            var word = __s1!.Letter + __s2!.Letter + __s3!.Letter + __s4!.Letter + __s5!.Letter + __s6!.Letter;
            SetState(-2);
            Trace.Concatenated(word);
            __builder.SetResult(word);
        }
        catch (Exception ex)
        {
            SetState(-2);
            __builder.SetException(ex);
        }
        finally
        {
            Trace.ExitMoveNext(entryState);
        }
    }

    public void SetStateMachine(IMyStateMachine sm) => __builder.SetStateMachine(sm);

    public static MyTask<string> Run(Action<LetterResult, int>? onLetter = null)
    {
        var sm = new FinalWordStateMachine
        {
            __state = -1,
            __builder = MyTaskBuilder<string>.Create(),
            LetterCompleted = onLetter
        };
        sm.__builder.Start(ref sm);
        return sm.__builder.Task;
    }
}
