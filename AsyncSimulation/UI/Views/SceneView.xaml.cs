using System.Collections.Specialized;
using System.Windows.Controls;
using AsyncSimulation.UI.ViewModels;

namespace AsyncSimulation.UI.Views;

public partial class SceneView : UserControl
{
    public SceneView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is SceneViewModel vm)
            vm.Rows.CollectionChanged += OnRowsChanged;
    }

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (RowList.Items.Count == 0) return;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, () =>
        {
            try { RowList.ScrollIntoView(RowList.Items[RowList.Items.Count - 1]); } catch { }
        });
    }
}
