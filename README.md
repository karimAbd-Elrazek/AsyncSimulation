# Async/Await Internals Simulator

A WPF teaching project that opens the **black box of `async`/`await`**: what the C# compiler actually generates, and which object holds a reference to which other object while the state machine runs. The async machinery (`MyTask`, `MyAwaiter`, `MyTaskBuilder`, `MySynchronizationContext`) is reimplemented from scratch — no `async`, `await`, or `Task` are used anywhere inside it.

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

The key idea: **every method on the heap belongs to an object**. The boxed state machine is an object (with type and fields). Each `Action` continuation delegate is an object whose `Target` field references the boxed SM and whose `Method` field names `MoveNext`. There are no naked methods floating in the heap.

---

## Quick Start

```bash
dotnet run --project AsyncSimulation
```

Click **Run**. Watch the rows accumulate. Each row is one event; the stack column and heap column on that row show the world *as of that event*.

---

## What the Compiler Does to Your Method

It deletes the body. It generates a **`struct`** that implements `IAsyncStateMachine`. The struct has:

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

`FinalWordStateMachine.cs` contains exactly this, hand‑written six times. The simulated method is:

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

## What the Heap Actually Looks Like

These are the heap objects you will see in the right column, in order of allocation:

| # | Type | Fields shown |
|---|---|---|
| 1 | `MyTask<string>` | `_completed`, `_continuations`, `_result` — the **outer** task returned to the caller. |
| 2 | `MyTask<LetterResult>` | `letter`, `_completed`, `_continuations`, `_result` — the inner task for `ComputeF`. |
| 3 | `Boxed FinalWordStateMachine` | `__state`, then `__s1..__s6` as each one is filled in over time. |
| 4 | `Action` | `Target = → Boxed SM #3`, `Method = MoveNext`. The continuation registered with the inner task. |
| 5–14 | …repeats for the other five letters. | |

Two important things about the heap entries:

1. **Methods do not exist alone on the heap.** The "continuation" that gets registered is an `Action` *object* on the heap. That object's `Target` field is what keeps the boxed state machine alive — when the `Action` fires, the runtime calls `Target.MoveNext()`. The `MoveNext` itself is just a method, defined once on the type.
2. **Field values change over time.** Watch the row that shows `Boxed FinalWordStateMachine`: on the row where the first `await` suspends, `__state` flips from `-1` to `0`. On the row where it resumes, `__s1` flips from `null` to `"F"`. Changed fields are highlighted yellow on the row that introduced the change.

---

## The Reference Graph (After First Suspension)

```
   MainViewModel  ─►  MyTask<string>      ◄────────┐  the OUTER task
                          │                        │  (held in 3 places)
                          ▼                        │
                continuations: [Action]            │
                          │                        │
                          └─ Action lambda         │
                                captures: { word display handler }


   Boxed FinalWordStateMachine                     │
   ─────────────────────────────                   │
   __state    : int                                │
   __s1..__s6 : LetterResult?  (filled in over time)│
   __aw1..__aw6 : MyAwaiter<LetterResult>          │
        │                                          │
        └─► _task : MyTask<LetterResult>  ─► (per-letter inner task)
                                                   │
   __builder : MyTaskBuilder<string>               │
        │                                          │
        ├─► _task  ─────────────────────────────────┘
        │
        └─► _box   ──┐
                     │  ◄── points back to itself (boxed.SetStateMachine(boxed)
                     │      writes this on first suspension; subsequent
                     │      suspensions reuse the box instead of re-boxing)
                     │
                     ▼
                 (same boxed SM — cycle!)
```

A few of the more interesting reference relationships:

- **The outer `MyTask<string>` is held in three places**: by the caller's local variable, by the boxed SM's builder (`__builder._task`), and indirectly by every continuation registered on it.
- **The boxed SM survives because the `Action` continuation holds it.** The `Action`'s `Target` field references the boxed SM. As long as the inner letter task's continuation list holds the `Action`, the boxed SM cannot be collected.
- **The cycle is intentional.** The boxed SM has a `__builder._box` field that points back to itself. The GC handles cycles fine. The cycle exists so that the *second* suspension does not re‑box: the builder sees `_box != null` and reuses the existing heap object.
- **The captured `SynchronizationContext` is what brings execution back to the UI thread.** When `OnCompleted` runs on the UI thread (during suspension), it captures `SynchronizationContext.Current` into a closure stored on the inner letter task. When the worker fires that closure, it calls `capturedCtx.Post(...)`, which delegates to `Dispatcher.BeginInvoke` — and the UI thread receives a message and runs `MoveNext` on its own thread.

---

## Project Layout

Folders are organised by **concern**, with one folder per architectural layer:

```
AsyncSimulation/
├── App.xaml(.cs), MainWindow.xaml(.cs), AssemblyInfo.cs   ← app shell
│
├── Async/                          ← simulated BCL: the "what async/await uses"
│   ├── IMyStateMachine.cs          ← MoveNext + SetStateMachine + SnapshotHeapFields
│   ├── MyAwaiter.cs                ← await bridge (ICriticalNotifyCompletion)
│   ├── MySynchronizationContext.cs ← Dispatcher router (~25 lines)
│   ├── MyTask.cs                   ← promise box; flips _completed; reports to Scene
│   ├── MyTaskBuilder.cs            ← orchestrator + boxing; allocates Action delegate
│   └── MyThreadPool.cs             ← 3 worker threads + queue
│
├── Diagnostics/                    ← visualisation service (separate concern from Async)
│   └── Scene.cs                    ← snapshot service: maintains live stack + heap; emits
│                                     a SceneSnapshot per Push/Pop/Alloc/Set/Log call
│
├── Demo/                           ← the specific teaching demo (6 awaits → "FINAL!")
│   ├── FinalWordStateMachine.cs    ← hand-written struct; calls Scene.Set on __state and __sN
│   ├── LetterComputation.cs        ← 6 dummy compute methods; allocates inner MyTask objects
│   └── Trace.cs                    ← all narrative log calls live here (DemoStarted, Resumed, …)
│
└── UI/                             ← MVVM presentation
    ├── Converters/Converters.cs    ← BoolToVisibility, CategoryToBrush
    ├── Themes/                     ← shared visual resources, merged in App.xaml
    │   ├── Colors.xaml             ← SolidColorBrush + FontFamily resources
    │   │                             (Surface.*, Border.*, Text.*, Stack.*, Heap.*,
    │   │                              New.*, Highlight.*, Code.*, Button.*)
    │   └── Styles.xaml             ← shared Styles (ToolbarButton, HeapFieldText,
    │                                 ColumnHeader, CodePanelLabel, CodeLine)
    ├── ViewModels/
    │   ├── ActualWindowViewModel.cs    ← S1..S6, Result properties; data-bound
    │   ├── MainViewModel.cs            ← Run/Clear commands, owns SceneVM + ActualVM
    │   ├── RelayCommand.cs             ← ICommand implementation
    │   ├── SceneViewModel.cs           ← exposes ObservableCollection<SnapshotRowVm>
    │   └── ViewModelBase.cs            ← INotifyPropertyChanged base
    └── Views/
        ├── ActualWindow.xaml(.cs)      ← 7 labels bound to ActualWindowViewModel
        └── SceneView.xaml(.cs)         ← the single-panel ListView
```

All views reference colors and fonts via `{StaticResource ...}` keyed by semantic name (e.g. `Stack.Accent`, `Heap.Bg`, `Highlight.Border`, `Font.Mono`). Changing a colour in `UI/Themes/Colors.xaml` updates every place it's used — no hex-hunting through XAML.

The async machinery in `Async/*` has **zero diagnostic logging inside it**. It only calls `Scene.Alloc(...)` when an object is allocated and `Scene.Set(...)` when a field is mutated — those calls live in `Async/` because allocation IS what the BCL does, and we want to record it. The narrative log lines (`Demo started`, `RESUMED after await #1 → got 'F'`, …) all live in `Demo/Trace.cs`, called only at clean boundaries (suspend / resume / done).

The boxed state machine is allocated with **all** lifted locals visible from the start (`__s1..__s6` initially `null`) and the **actual current `__state`** (not a hardcoded `-1`) — `IMyStateMachine.SnapshotHeapFields()` returns the values, and `MyTaskBuilder.EnsureBoxed` uses them. This way the heap card shows the boxed object's true contents at the moment of boxing, and as each `__sN` is filled in over the next 12 seconds you see the field flip to `"F"`, `"I"`, … with a yellow highlight on the row that introduced the change.

---

## Mapping to the Real BCL

| This project | Real BCL |
|---|---|
| `MyTask<T>` | `Task<T>` |
| `MyAwaiter<T>` | `TaskAwaiter<T>` |
| `MyTaskBuilder<T>` | `AsyncTaskMethodBuilder<T>` |
| `IMyStateMachine` | `IAsyncStateMachine` |
| `MySynchronizationContext` | `DispatcherSynchronizationContext` |
| `MyThreadPool` | `ThreadPool` (sized dynamically by hill‑climbing in the real one) |
| `FinalWordStateMachine` | Compiler‑generated `<BuildFinalWordAsync>d__0` |
| `Scene` | *(no equivalent — visualization only)* |

Simplifications relative to the BCL:

- **State machine is always a `struct`.** Release‑mode compilers emit a struct; Debug emits a class. This project picks struct so the boxing story is real and visible.
- **`AwaitUnsafeOnCompleted` skips `ExecutionContext` capture.** The simulator implements both `OnCompleted` and `UnsafeOnCompleted`, but they currently do the same thing.
- **`MyTaskBuilder` always allocates a `MyTask` up front.** The real `AsyncTaskMethodBuilder<T>` is lazier — it can return `Task.FromResult(...)` for methods that complete synchronously without ever creating a task object.

---

## Glossary

| Term | Definition |
|---|---|
| **State machine** | A struct (Release) or class (Debug) generated by the compiler from an async method. Holds all lifted locals as fields and tracks the current suspension point via `__state`. |
| **Builder** | The struct that drives the state machine: creates the task, calls `MoveNext`, boxes the SM on first suspension, registers continuations, completes the task. |
| **Awaiter** | The bridge between `await` and a task: `IsCompleted`, `GetResult()`, `OnCompleted(Action)`. |
| **Lifted local** | A local variable that crosses an `await` boundary — turned into a field on the state machine struct so it survives the stack unwind. |
| **Boxing** | Copying a value type (struct) from stack to heap. Happens once per async method invocation, on first suspension. |
| **Continuation** | The `Action` object registered via `OnCompleted` — wraps `MoveNext` (or a posted version of it). When the task completes, every continuation fires. |
| **`SynchronizationContext`** | A thread‑routing primitive captured by the awaiter at `await` time. Its `Post(callback)` queues `MoveNext` back to the original thread (typically the UI thread via `Dispatcher.BeginInvoke`). |
| **Scene snapshot** | One row in the simulator's UI list — a structured record containing description + full stack + full heap at that moment. Each event (push, pop, alloc, set, log) emits a new snapshot. |
| **BCL** | Base Class Library — built‑in .NET types: `Task`, `Thread`, the entire async infrastructure. |
