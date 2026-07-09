using System;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Controls;
using Avalonia.Controls.Primitives; 

namespace Tshin.Views;

public partial class ColorPickerPopup : Window
{
    private bool _isUpdating;
    private double _lastHue;

    private static readonly (string name, Color color)[] NamedSwatches =
    [
        ("Red", Colors.Red),
        ("Crimson", Color.FromArgb(255, 220, 20, 60)),
        ("OrangeRed", Colors.OrangeRed),
        ("Orange", Colors.Orange),
        ("Gold", Colors.Gold),
        ("Yellow", Colors.Yellow),
        ("LimeGreen", Color.FromArgb(255, 50, 205, 50)),
        ("Green", Colors.Green),
        ("Teal", Colors.Teal),
        ("Cyan", Colors.Cyan),
        ("DodgerBlue", Color.FromArgb(255, 30, 144, 255)),
        ("Blue", Colors.Blue),
        ("Indigo", Colors.Indigo),
        ("Purple", Colors.Purple),
        ("Magenta", Colors.Magenta),
        ("HotPink", Color.FromArgb(255, 255, 105, 180)),
        ("Brown", Colors.Brown),
        ("White", Colors.White),
        ("Silver", Colors.Silver),
        ("Gray", Colors.Gray),
        ("Black", Colors.Black),
    ];

    public ColorPickerPopup()
    {
        InitializeComponent();
        BuildSwatches();

        HueSlider.ValueChanged += OnSliderChanged;
        SaturationSlider.ValueChanged += OnSliderChanged;
        BrightnessSlider.ValueChanged += OnSliderChanged;
        HexInput.TextChanged += OnHexTextChanged;

        _lastHue = 0;
        UpdatePreviewFromHsv(0, 0, 100);
    }

    private void BuildSwatches()
    {
        foreach (var (name, color) in NamedSwatches)
        {
            var border = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(color),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 0, 0, 0)),
                BorderThickness = new Thickness(1),
                Tag = name,
                Cursor = new Cursor(StandardCursorType.Hand),
                Margin = new Thickness(3),
            };

            ToolTip.SetTip(border, new ToolTip { Content = $"{name} ({color.ToString()})" });

            border.PointerPressed += (_, _) =>
            {
                _isUpdating = true;
                var (h, s, v) = RgbToHsv(color);
                if (s > 0 && v > 0) _lastHue = h;
                HueSlider.Value = h;
                SaturationSlider.Value = s;
                BrightnessSlider.Value = v;
                _isUpdating = false;
                SyncFromSlidersToColor();
            };

            SwatchPanel.Children.Add(border);
        }
    }

    private void OnSliderChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdating) return;
        SyncFromSlidersToColor();
    }

    private void SyncFromSlidersToColor()
    {
        var h = HueSlider.Value;
        var s = SaturationSlider.Value / 100.0;
        var v = BrightnessSlider.Value / 100.0;
        if (s > 0 && v > 0) _lastHue = h;
        var (r, g, b) = HsvToRgb(h, s, v);
        var color = Color.FromArgb(255, (byte)r, (byte)g, (byte)b);

        PreviewSwatch.Background = new SolidColorBrush(color);

        _isUpdating = true;
        HexInput.Text = color.ToString();
        _isUpdating = false;
    }

    private void OnHexTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isUpdating) return;
        var hex = HexInput.Text?.Trim();
        if (string.IsNullOrEmpty(hex)) return;

        if (Color.TryParse(hex, out var color))
        {
            PreviewSwatch.Background = new SolidColorBrush(color);
            _isUpdating = true;
            var (h, s, v) = RgbToHsv(color);
            if (s > 0 && v > 0) _lastHue = h;
            HueSlider.Value = s > 0 && v > 0 ? h : _lastHue;
            SaturationSlider.Value = s;
            BrightnessSlider.Value = v;
            _isUpdating = false;
        }
    }

    private void UpdatePreviewFromHsv(double h, double s, double v)
    {
        var (r, g, b) = HsvToRgb(h, s / 100.0, v / 100.0);
        var color = Color.FromArgb(255, (byte)r, (byte)g, (byte)b);
        PreviewSwatch.Background = new SolidColorBrush(color);
        HexInput.Text = color.ToString();
    }

    private static (double h, double s, double v) RgbToHsv(Color c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        var h = 0.0;
        if (delta > 0)
        {
            if (Math.Abs(max - r) < 0.001)
                h = 60 * (((g - b) / delta) % 6);
            else if (Math.Abs(max - g) < 0.001)
                h = 60 * (((b - r) / delta) + 2);
            else
                h = 60 * (((r - g) / delta) + 4);
        }
        if (h < 0) h += 360;

        var s = max > 0 ? delta / max * 100 : 0;
        var v = max * 100;
        return (h, s, v);
    }

    private static (int r, int g, int b) HsvToRgb(double h, double s, double v)
    {
        var c = v * s;
        var x = c * (1 - Math.Abs((h / 60) % 2 - 1));
        var m = v - c;

        double r, g, b;
        if (h < 60) { r = c; g = x; b = 0; }
        else if (h < 120) { r = x; g = c; b = 0; }
        else if (h < 180) { r = 0; g = c; b = x; }
        else if (h < 240) { r = 0; g = x; b = c; }
        else if (h < 300) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }

        return ((int)((r + m) * 255), (int)((g + m) * 255), (int)((b + m) * 255));
    }

    private void OnApplyClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var hex = HexInput.Text?.Trim();
        if (!string.IsNullOrEmpty(hex) && Color.TryParse(hex, out var color))
        {
            Close(color.ToString());
            return;
        }
        Close(null);
    }

    private void OnCancelClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(null);
    }
}
