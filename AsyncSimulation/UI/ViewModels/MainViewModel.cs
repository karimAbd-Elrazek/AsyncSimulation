using System.Windows.Input;
using System.Windows.Threading;
using AsyncSimulation.Demo;

namespace AsyncSimulation.UI.ViewModels;

public class MainViewModel : ViewModelBase
{
    public SceneViewModel SceneVM { get; }
    public ActualWindowViewModel ActualVM { get; }

    public ICommand RunDemoCommand { get; }
    public ICommand ClearCommand { get; }

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (SetProperty(ref _isRunning, value))
                ((RelayCommand)RunDemoCommand).RaiseCanExecuteChanged();
        }
    }

    private string _statusText = "Ready";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public MainViewModel(Dispatcher dispatcher)
    {
        SceneVM = new SceneViewModel(dispatcher);
        ActualVM = new ActualWindowViewModel();

        RunDemoCommand = new RelayCommand(RunDemo, () => !IsRunning);
        ClearCommand = new RelayCommand(ClearAll);
    }

    private void RunDemo()
    {
        ClearAll();
        IsRunning = true;
        StatusText = "Running 6 awaits (2s each)...";

        Trace.DemoStarted();

        var task = FinalWordStateMachine.Run(onLetter: (result, index) =>
            ActualVM.SetSlot(index, $"\"{result.Letter}\"  (T{result.ComputedOnThreadId}, {result.ElapsedMs}ms)"));

        // Fires LATER on a fresh dispatcher pump — RunDemo has long since returned by then.
        task.GetAwaiter().OnCompleted(() =>
        {
            var word = task.Result;
            IsRunning = false;
            StatusText = $"COMPLETE → \"{word}\"";
            ActualVM.SetFinal($"\"{word}\"");
            Trace.DemoComplete(word);
        });

        // RunDemo's stack frame goes away the moment we return to our caller.
        Trace.DemoReturned();
    }

    private void ClearAll()
    {
        SceneVM.Clear();
        ActualVM.Reset();
        StatusText = "Ready";
    }
}
