namespace AsyncSimulation.Diagnostics;

public record HeapField(string Name, string Value);

public record HeapObjectSnapshot(int Id, string TypeName, IReadOnlyList<HeapField> Fields);

public record StackFrameSnapshot(string Name, int ThreadId);

public record SceneSnapshot(
    DateTime Timestamp,
    int ThreadId,
    string Category,
    string Description,
    IReadOnlyList<StackFrameSnapshot> Stack,
    IReadOnlyList<HeapObjectSnapshot> Heap,
    int? NewObjectId,
    int? ChangedObjectId,
    string? ChangedFieldName
);

/// <summary>
/// Single source of truth for everything we visualize. Each Push/Pop/Alloc/Set/Log
/// updates the live state and emits a SceneSnapshot with the full stack + heap at
/// that moment — the UI just lists snapshots, one row per event.
///
/// IMPORTANT: heap entries are OBJECTS (type + fields). Methods do not live alone
/// in the heap. The MoveNext continuation is an Action delegate object whose
/// Target field references the boxed state machine.
/// </summary>
public static class Scene
{
    private static readonly object Lock = new();
    private static int _nextId = 1;
    private static readonly List<StackFrameSnapshot> Stack = new();
    private static readonly List<MutableHeapObject> Heap = new();

    public static event Action<SceneSnapshot>? SnapshotAdded;

    private class MutableHeapObject
    {
        public int Id;
        public string TypeName = "";
        public List<string> FieldOrder = new();
        public Dictionary<string, string> Fields = new();
    }

    public static void Push(string frameName)
    {
        SceneSnapshot snap;
        lock (Lock)
        {
            Stack.Add(new StackFrameSnapshot(frameName, Environment.CurrentManagedThreadId));
            snap = Build("Stack", $"PUSH {frameName}", null, null, null);
        }
        SnapshotAdded?.Invoke(snap);
    }

    public static void Pop(string frameName)
    {
        SceneSnapshot snap;
        lock (Lock)
        {
            for (int i = Stack.Count - 1; i >= 0; i--)
            {
                if (Stack[i].Name == frameName) { Stack.RemoveAt(i); break; }
            }
            snap = Build("Stack", $"POP  {frameName}", null, null, null);
        }
        SnapshotAdded?.Invoke(snap);
    }

    public static int Alloc(string typeName, params (string name, string value)[] initialFields)
    {
        SceneSnapshot snap;
        int id;
        lock (Lock)
        {
            id = _nextId++;
            var obj = new MutableHeapObject { Id = id, TypeName = typeName };
            foreach (var (n, v) in initialFields)
            {
                obj.Fields[n] = v;
                obj.FieldOrder.Add(n);
            }
            Heap.Add(obj);
            snap = Build("Heap", $"ALLOC {typeName}#{id}", id, null, null);
        }
        SnapshotAdded?.Invoke(snap);
        return id;
    }

    public static void Set(int objId, string fieldName, string value)
    {
        if (objId <= 0) return;
        SceneSnapshot? snap = null;
        lock (Lock)
        {
            var obj = Heap.FirstOrDefault(o => o.Id == objId);
            if (obj is null) return;
            if (!obj.Fields.ContainsKey(fieldName)) obj.FieldOrder.Add(fieldName);
            obj.Fields[fieldName] = value;
            snap = Build("Mutation", $"{obj.TypeName}#{objId}.{fieldName} = {value}", null, objId, fieldName);
        }
        if (snap is not null) SnapshotAdded?.Invoke(snap);
    }

    public static void Log(string category, string message)
    {
        SceneSnapshot snap;
        lock (Lock)
        {
            snap = Build(category, message, null, null, null);
        }
        SnapshotAdded?.Invoke(snap);
    }

    public static void Reset()
    {
        lock (Lock)
        {
            Stack.Clear();
            Heap.Clear();
            _nextId = 1;
        }
    }

    private static SceneSnapshot Build(string category, string description, int? newObjId, int? changedObjId, string? changedField)
    {
        // Caller MUST hold Lock.
        var stack = Stack.Select(f => f).ToList();
        var heap = Heap.Select(o => new HeapObjectSnapshot(
            o.Id,
            o.TypeName,
            o.FieldOrder.Select(n => new HeapField(n, o.Fields[n])).ToList()
        )).ToList();

        return new SceneSnapshot(
            DateTime.Now,
            Environment.CurrentManagedThreadId,
            category,
            description,
            stack,
            heap,
            newObjId,
            changedObjId,
            changedField
        );
    }
}
