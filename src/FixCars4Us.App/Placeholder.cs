using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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
        textBox.SizeChanged -= OnSizeChanged;
        textBox.SizeChanged += OnSizeChanged;

        UpdatePlaceholder(textBox, null);
    }

    private static void OnLoaded(object sender, RoutedEventArgs e) => UpdatePlaceholder(sender, null);
    private static void OnSizeChanged(object sender, SizeChangedEventArgs e) => UpdatePlaceholder(sender, null);

    private static void UpdatePlaceholder(object? sender, TextChangedEventArgs? e)
    {
        if (sender is not TextBox textBox) return;

        var layer = AdornerLayer.GetAdornerLayer(textBox);
        if (layer == null) return;

        var existing = layer.GetAdorners(textBox)?.OfType<PlaceholderAdorner>().ToArray();
        if (existing != null)
        {
            foreach (var adorner in existing) layer.Remove(adorner);
        }

        var placeholderText = GetText(textBox);
        if (string.IsNullOrEmpty(textBox.Text) && !string.IsNullOrEmpty(placeholderText))
        {
            layer.Add(new PlaceholderAdorner(textBox, placeholderText));
        }
    }
}

internal class PlaceholderAdorner : Adorner
{
    private readonly string _text;

    public PlaceholderAdorner(UIElement adornedElement, string text) : base(adornedElement)
    {
        _text = text;
        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var textBox = (TextBox)AdornedElement;
        var typeface = new Typeface(textBox.FontFamily, FontStyles.Italic, FontWeights.Normal, FontStretches.Normal);
        var formattedText = new FormattedText(
            _text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            textBox.FontSize,
            Brushes.Gray,
            VisualTreeHelper.GetDpi(textBox).PixelsPerDip);

        var y = (textBox.ActualHeight - formattedText.Height) / 2;
        drawingContext.DrawText(formattedText, new Point(textBox.Padding.Left + 4, System.Math.Max(0, y)));
    }
}
