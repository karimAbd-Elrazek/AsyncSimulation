using System.Collections.ObjectModel;
using System.Windows.Threading;
using AsyncSimulation.Diagnostics;

namespace AsyncSimulation.UI.ViewModels;

public class HeapFieldRowVm
{
    public string Name { get; init; } = "";
    public string Value { get; init; } = "";
    public bool Changed { get; init; }
    public string Display => $"{Name}: {Value}";
}

public class HeapObjectRowVm
{
    public int Id { get; init; }
    public string TypeName { get; init; } = "";
    public string Header => $"{TypeName} #{Id}";
    public bool IsNew { get; init; }
    public IReadOnlyList<HeapFieldRowVm> Fields { get; init; } = Array.Empty<HeapFieldRowVm>();
}

public class StackFrameRowVm
{
    public string Name { get; init; } = "";
    public int ThreadId { get; init; }
    public string ThreadLabel => $"T{ThreadId}";
}

public class SnapshotRowVm
{
    public string Time { get; init; } = "";
    public int ThreadId { get; init; }
    public string ThreadLabel => $"T{ThreadId}";
    public string Category { get; init; } = "";
    public string Description { get; init; } = "";
    public IReadOnlyList<StackFrameRowVm> Stack { get; init; } = Array.Empty<StackFrameRowVm>();
    public IReadOnlyList<HeapObjectRowVm> Heap { get; init; } = Array.Empty<HeapObjectRowVm>();
}

public class SceneViewModel : ViewModelBase
{
    private readonly Dispatcher _dispatcher;

    public ObservableCollection<SnapshotRowVm> Rows { get; } = new();

    public SceneViewModel(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        Scene.SnapshotAdded += OnSnapshot;
    }

    private void OnSnapshot(SceneSnapshot snap)
        => _dispatcher.BeginInvoke(DispatcherPriority.Background, () => Rows.Add(ToRow(snap)));

    private static SnapshotRowVm ToRow(SceneSnapshot s) => new()
    {
        Time = s.Timestamp.ToString("HH:mm:ss.fff"),
        ThreadId = s.ThreadId,
        Category = s.Category,
        Description = s.Description,
        Stack = s.Stack.Select(f => new StackFrameRowVm
        {
            Name = f.Name,
            ThreadId = f.ThreadId
        }).ToList(),
        Heap = s.Heap.Select(o => new HeapObjectRowVm
        {
            Id = o.Id,
            TypeName = o.TypeName,
            IsNew = s.NewObjectId == o.Id,
            Fields = o.Fields.Select(f => new HeapFieldRowVm
            {
                Name = f.Name,
                Value = f.Value,
                Changed = s.ChangedObjectId == o.Id && s.ChangedFieldName == f.Name
            }).ToList()
        }).ToList()
    };

    public void Clear() => Rows.Clear();
}
