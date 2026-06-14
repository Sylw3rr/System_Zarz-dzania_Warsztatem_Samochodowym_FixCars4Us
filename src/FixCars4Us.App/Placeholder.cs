using System.Windows;

namespace FixCars4Us.App;

public class Placeholder : DependencyObject
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register("Text", typeof(string), typeof(Placeholder), new PropertyMetadata(string.Empty));

    public static string GetText(DependencyObject obj) => (string)obj.GetValue(TextProperty);
    public static void SetText(DependencyObject obj, string value) => obj.SetValue(TextProperty, value);
}
