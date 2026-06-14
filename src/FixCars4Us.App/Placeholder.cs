using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FixCars4Us.App;

public class Placeholder : DependencyObject
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register("Text", typeof(string), typeof(Placeholder),
            new PropertyMetadata(string.Empty, OnTextChanged));

    public static string GetText(DependencyObject obj) => (string)obj.GetValue(TextProperty);
    public static void SetText(DependencyObject obj, string value) => obj.SetValue(TextProperty, value);

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox) return;

        textBox.TextChanged -= UpdatePlaceholder;
        textBox.TextChanged += UpdatePlaceholder;
        textBox.Loaded -= OnLoaded;
        textBox.Loaded += OnLoaded;

        UpdatePlaceholder(textBox, null);
    }

    private static void OnLoaded(object sender, RoutedEventArgs e) => UpdatePlaceholder(sender, null);

    private static void UpdatePlaceholder(object? sender, TextChangedEventArgs? e)
    {
        if (sender is not TextBox textBox) return;
        var placeholderText = GetText(textBox);

        if (string.IsNullOrEmpty(textBox.Text) && !string.IsNullOrEmpty(placeholderText))
        {
            textBox.Background = new VisualBrush
            {
                AlignmentX = AlignmentX.Left,
                AlignmentY = AlignmentY.Center,
                Stretch = Stretch.None,
                Visual = new TextBlock
                {
                    Text = placeholderText,
                    Foreground = Brushes.Gray,
                    FontStyle = FontStyles.Italic,
                    Margin = new Thickness(4, 0, 0, 0)
                }
            };
        }
        else
        {
            textBox.ClearValue(TextBox.BackgroundProperty);
        }
    }
}
