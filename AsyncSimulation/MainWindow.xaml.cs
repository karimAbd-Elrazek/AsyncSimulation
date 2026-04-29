using System.Windows;
using AsyncSimulation.Async;
using AsyncSimulation.UI.ViewModels;

namespace AsyncSimulation;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        MySynchronizationContext.Install(Dispatcher);
        MyThreadPool.Initialize(3);

        DataContext = new MainViewModel(Dispatcher);
    }
}
