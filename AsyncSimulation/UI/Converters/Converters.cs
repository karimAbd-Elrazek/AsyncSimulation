using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace AsyncSimulation.UI.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && b ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class CategoryToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string cat
            ? cat switch
            {
                "Main"          => new SolidColorBrush(Color.FromRgb(255, 144, 232)), // Pink
                "StateMachine"  => new SolidColorBrush(Color.FromRgb(99, 102, 241)),  // Indigo
                "Stack"         => new SolidColorBrush(Color.FromRgb(2, 132, 199)),   // Sky blue
                "Heap"          => new SolidColorBrush(Color.FromRgb(217, 119, 6)),   // Amber
                "Mutation"      => new SolidColorBrush(Color.FromRgb(220, 38, 38)),   // Red
                _               => new SolidColorBrush(Color.FromRgb(156, 163, 175)),  // Gray
            }
            : (object)Brushes.Gray;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
