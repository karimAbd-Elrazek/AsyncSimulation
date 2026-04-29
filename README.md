# Async/Await Internals Simulator

A WPF teaching project that opens the black box of `async`/`await`: what the C# compiler actually generates, and which object holds a reference to which other object while the state machine runs. The async machinery (`MyTask`, `MyAwaiter`, `MyTaskBuilder`, `MySynchronizationContext`) is reimplemented from scratch — no `async`, `await`, or `Task` are used anywhere inside it.

The window has a **single panel**: a list. Each row is a moment in time. Each row shows what just happened *and* the full shape of the **stack** and **heap** at that moment, side by side. As you scroll down, you see the program's runtime state evolve event by event.

```
┌──────────────────────────────────────────────────────────────────────────────────────┐
│ TIME      WHO      WHAT JUST HAPPENED              STACK           HEAP              │
├──────────────────────────────────────────────────────────────────────────────────────┤
│ 10:00.01 [Main]    Demo started                    ┌──────────┐    (empty)           │
│          T1                                        │ RunDemo  │                      │
│                                                    └──────────┘                      │
├──────────────────────────────────────────────────────────────────────────────────────┤
│ 10:00.01 [Heap]    ALLOC MyTask<string>#1          ┌──────────┐    ┌─────────────┐   │
│          T1                                        │ RunDemo  │    │ MyTask<...>#1│  │
│                                                    └──────────┘    │ _completed:F │  │
│                                                                    │ _continuat:[]│  │
│                                                                    └─────────────┘   │
├──────────────────────────────────────────────────────────────────────────────────────┤
│ 10:00.01 [Heap]    ALLOC Boxed FinalWordSM#3       ┌──────────┐    ┌────┐ ┌────┐ ┌──┐│
│          T1                                        │ MoveNext │    │MT#1│ │MT<L│ │SM#3│
│                                                    │ RunDemo  │    │... │ │R>#2│ │... │
│                                                    └──────────┘    └────┘ └────┘ └──┘│
└──────────────────────────────────────────────────────────────────────────────────────┘
```

---

## Quick Start

```bash
dotnet run --project AsyncSimulation
```

Click **Run**. Watch the rows accumulate. Each row is one event; the stack and heap columns show the world *as of that event*.

---

## What the Compiler Does to Your Method

It deletes the body and generates a **`struct`** that implements `IAsyncStateMachine`. The struct has:

- a field for every **local that crosses an `await` boundary** (a "lifted local"),
- a field for every `await`'s **awaiter**,
- an `int __state`,
- an embedded **`AsyncTaskMethodBuilder<T> __builder`** that drives everything.

Your method body becomes the struct's `MoveNext()`. Each `await x` is lowered to:

```csharp
__awN = x.GetAwaiter();
if (!__awN.IsCompleted)
{
    __state = N;
    __builder.AwaitUnsafeOnCompleted(ref __awN, ref this);
    return;                       // ← method literally returns; stack unwinds
}
AfterN:
var result = __awN.GetResult();   // ← jumped to here on resume
```

`FinalWordStateMachine.cs` contains exactly this, hand-written six times. The simulated method is:

```csharp
async MyTask<string> BuildFinalWordAsync()
{
    var s1 = await ComputeF();
    var s2 = await ComputeI();
    var s3 = await ComputeN();
    var s4 = await ComputeA();
    var s5 = await ComputeL();
    var s6 = await ComputeBang();
    return s1 + s2 + s3 + s4 + s5 + s6;     // "FINAL!"
}
```

---

## Heap Objects

These are the heap objects visible in the right column, in order of allocation:

| # | Type | Fields shown |
|---|---|---|
| 1 | `MyTask<string>` | `_completed`, `_continuations`, `_result` — the outer task returned to the caller. |
| 2 | `MyTask<LetterResult>` | `letter`, `_completed`, `_continuations`, `_result` — the inner task for `ComputeF`. |
| 3 | `Boxed FinalWordStateMachine` | `__state`, then `__s1..__s6` filled in over time. |
| 4 | `Action` | `Target → Boxed SM #3`, `Method = MoveNext`. The continuation registered with the inner task. |
| 5–14 | Repeats for the remaining five letters. | |

Field values change over time. Watch `Boxed FinalWordStateMachine`: when the first `await` suspends, `__state` flips from `-1` to `0`; when it resumes, `__s1` flips from `null` to `"F"`. Changed fields are highlighted yellow on the row that introduced the change.

---

## Reference Graph (After First Suspension)

```
   MainViewModel  ─►  MyTask<string>
                          │
                          ▼
                continuations: [Action]
                          │
                          └─ Action lambda
                                captures: { word display handler }


   Boxed FinalWordStateMachine
   ─────────────────────────────
   __state    : int
   __s1..__s6 : LetterResult?  (filled in over time)
   __aw1..__aw6 : MyAwaiter<LetterResult>
        │
        └─► _task : MyTask<LetterResult>  ─► (per-letter inner task)

   __builder : MyTaskBuilder<string>
        │
        ├─► _task  ──────────────────────────► MyTask<string>  (the outer task)
        │
        └─► _box   ──┐
                     │
                     ▼
                 (same boxed SM — cycle)
```

Notable reference relationships:

- The outer `MyTask<string>` is held by the caller's local variable, by `__builder._task`, and indirectly by every continuation registered on it.
- The boxed SM survives because the `Action` continuation's `Target` field references it. As long as the inner letter task's continuation list holds that `Action`, the boxed SM stays alive.
- The `__builder._box` cycle is intentional — on second suspension, the builder sees `_box != null` and reuses the existing heap object instead of re-boxing.
- The captured `SynchronizationContext` is what routes execution back to the UI thread. When the worker fires the continuation, it calls `capturedCtx.Post(...)`, which delegates to `Dispatcher.BeginInvoke`, and the UI thread runs `MoveNext`.

---

## Project Layout

```
AsyncSimulation/
├── App.xaml(.cs), MainWindow.xaml(.cs), AssemblyInfo.cs   ← app shell
│
├── Async/                          ← simulated BCL
│   ├── IMyStateMachine.cs
│   ├── MyAwaiter.cs
│   ├── MySynchronizationContext.cs
│   ├── MyTask.cs
│   ├── MyTaskBuilder.cs
│   └── MyThreadPool.cs
│
├── Diagnostics/
│   └── Scene.cs                    ← snapshot service: maintains live stack + heap
│
├── Demo/
│   ├── FinalWordStateMachine.cs    ← hand-written struct (6 awaits)
│   ├── LetterComputation.cs        ← 6 dummy compute methods
│   └── Trace.cs                    ← all narrative log calls
│
└── UI/
    ├── Converters/Converters.cs
    ├── Themes/
    │   ├── Colors.xaml
    │   └── Styles.xaml
    ├── ViewModels/
    │   ├── ActualWindowViewModel.cs
    │   ├── MainViewModel.cs
    │   ├── RelayCommand.cs
    │   ├── SceneViewModel.cs
    │   └── ViewModelBase.cs
    └── Views/
        ├── ActualWindow.xaml(.cs)
        └── SceneView.xaml(.cs)
```

The async machinery in `Async/*` calls `Scene.Alloc(...)` on allocation and `Scene.Set(...)` on field mutation. Narrative log lines (`Demo started`, `RESUMED after await #1 → got 'F'`, etc.) live exclusively in `Demo/Trace.cs`.

The boxed state machine is allocated with all lifted locals visible from the start (`__s1..__s6` initially `null`) using the actual current `__state` value. `IMyStateMachine.SnapshotHeapFields()` returns the values; `MyTaskBuilder.EnsureBoxed` uses them to initialize the heap card correctly at the moment of boxing.

---

## Mapping to the Real BCL

| This project | Real BCL |
|---|---|
| `MyTask<T>` | `Task<T>` |
| `MyAwaiter<T>` | `TaskAwaiter<T>` |
| `MyTaskBuilder<T>` | `AsyncTaskMethodBuilder<T>` |
| `IMyStateMachine` | `IAsyncStateMachine` |
| `MySynchronizationContext` | `DispatcherSynchronizationContext` |
| `MyThreadPool` | `ThreadPool` |
| `FinalWordStateMachine` | Compiler-generated `<BuildFinalWordAsync>d__0` |
| `Scene` | *(visualization only — no BCL equivalent)* |

**Simplifications:**

- State machine is always a `struct`. Release-mode compilers emit a struct; Debug emits a class. This project uses struct so the boxing story is real and visible.
- `AwaitUnsafeOnCompleted` skips `ExecutionContext` capture. Both `OnCompleted` and `UnsafeOnCompleted` are implemented but currently behave identically.
- `MyTaskBuilder` always allocates a `MyTask` up front. The real `AsyncTaskMethodBuilder<T>` can return `Task.FromResult(...)` for synchronously-completing methods without allocating.

---

## Glossary

| Term | Definition |
|---|---|
| **State machine** | A struct (Release) or class (Debug) generated by the compiler from an async method. Holds lifted locals as fields and tracks the current suspension point via `__state`. |
| **Builder** | Drives the state machine: creates the task, calls `MoveNext`, boxes the SM on first suspension, registers continuations, completes the task. |
| **Awaiter** | Bridge between `await` and a task: `IsCompleted`, `GetResult()`, `OnCompleted(Action)`. |
| **Lifted local** | A local variable that crosses an `await` boundary — promoted to a field on the state machine struct so it survives the stack unwind. |
| **Boxing** | Copying a value type from stack to heap. Happens once per async method invocation, on first suspension. |
| **Continuation** | The `Action` registered via `OnCompleted`. When the task completes, every registered continuation fires. |
| **SynchronizationContext** | A thread-routing primitive captured at `await` time. Its `Post(callback)` queues `MoveNext` back to the original thread. |
| **Scene snapshot** | One row in the simulator's list — description + full stack + full heap at that moment. |
| **BCL** | Base Class Library — built-in .NET types: `Task`, `Thread`, the full async infrastructure. |
