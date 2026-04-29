namespace AsyncSimulation.Async;

public interface IMyStateMachine
{
    void MoveNext();
    void SetStateMachine(IMyStateMachine stateMachine);

    /// <summary>
    /// Field values to record on the boxed heap object when this state machine
    /// is first boxed. Lets the visualizer show lifted locals from the start
    /// (initially null) and the actual __state at the moment of boxing.
    /// </summary>
    IEnumerable<(string Name, string Value)> SnapshotHeapFields();
}
