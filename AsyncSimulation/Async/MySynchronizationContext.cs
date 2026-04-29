using System.Windows.Threading;

namespace AsyncSimulation.Async;

public class MySynchronizationContext : SynchronizationContext
{
    private readonly Dispatcher _dispatcher;

    public static MySynchronizationContext? CurrentCustom { get; private set; }

    private MySynchronizationContext(Dispatcher dispatcher) { _dispatcher = dispatcher; }

    public static void Install(Dispatcher dispatcher)
    {
        var ctx = new MySynchronizationContext(dispatcher);
        CurrentCustom = ctx;
        SetSynchronizationContext(ctx);
    }

    public override void Post(SendOrPostCallback d, object? state)
        => _dispatcher.BeginInvoke(() => d(state));

    public override void Send(SendOrPostCallback d, object? state)
        => _dispatcher.Invoke(() => d(state));

    public override SynchronizationContext CreateCopy() => new MySynchronizationContext(_dispatcher);
}
