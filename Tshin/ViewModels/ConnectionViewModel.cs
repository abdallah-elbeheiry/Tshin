using System;
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace Tshin.ViewModels;

/// <summary>
/// A bezier wire from one choice's output pin to a target node's input pin.
/// Recomputes its geometry whenever either endpoint node moves.
/// Alternating colours per connection and an arrowhead at the target end
/// make distinct wires easy to tell apart.
/// </summary>
public sealed class ConnectionViewModel : ViewModelBase, IDisposable
{
    /// <summary>Number of distinct wire hues (WireBrush0..WireBrush6).</summary>
    public const int PaletteCount = 7;

    private readonly int _colorIndex;

    public NodeViewModel Source { get; }
    public NodeViewModel Target { get; }
    public int ChoiceIndex { get; }

    /// <summary>
    /// Resolved live from the theme dictionary so it tracks a light/dark switch instead of
    /// being frozen at construction. The index is supplied deterministically by the editor.
    /// </summary>
    public IBrush Stroke => ResolveStroke();

    public ConnectionViewModel(NodeViewModel source, NodeViewModel target, int choiceIndex, int colorIndex)
    {
        Source = source;
        Target = target;
        ChoiceIndex = choiceIndex;
        _colorIndex = ((colorIndex % PaletteCount) + PaletteCount) % PaletteCount;
        Source.PropertyChanged += OnEndpointChanged;
        Target.PropertyChanged += OnEndpointChanged;

        if (Application.Current is { } app)
            app.ActualThemeVariantChanged += OnThemeVariantChanged;
    }

    private IBrush ResolveStroke()
    {
        var key = "WireBrush" + _colorIndex;
        if (Application.Current is { } app &&
            app.TryGetResource(key, app.ActualThemeVariant, out var res) && res is IBrush b)
            return b;
        return Brushes.Gray;
    }

    private void OnThemeVariantChanged(object? sender, EventArgs e)
        => OnPropertyChanged(nameof(Stroke));

    private void OnEndpointChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(NodeViewModel.X) or nameof(NodeViewModel.Y))) return;
        OnPropertyChanged(nameof(StartPoint));
        OnPropertyChanged(nameof(EndPoint));
        OnPropertyChanged(nameof(PathData));
        OnPropertyChanged(nameof(Geometry));
        OnPropertyChanged(nameof(ArrowTip));
    }

    // Wires are drawn in the same (biased) canvas space as the node cards, so the pin
    // coordinates carry the same CanvasBias offset — see NodeLayout.CanvasBias.
    public Point StartPoint => new(
        NodeLayout.OutputPinX(Source) + NodeLayout.CanvasBias,
        NodeLayout.OutputPinY(Source, ChoiceIndex) + NodeLayout.CanvasBias);
    public Point EndPoint => new(
        NodeLayout.InputPinX(Target) + NodeLayout.CanvasBias,
        NodeLayout.InputPinY(Target) + NodeLayout.CanvasBias);

    public string PathData => BuildPath(StartPoint, EndPoint);

    public Geometry Geometry => Geometry.Parse(PathData);

    /// <summary>Cubic bezier path string (invariant culture, parseable by Path.Data).</summary>
    public static string BuildPath(Point s, Point e)
    {
        var dx = Math.Max(40, Math.Abs(e.X - s.X) * 0.5);
        return string.Create(CultureInfo.InvariantCulture,
            $"M {s.X},{s.Y} C {s.X + dx},{s.Y} {e.X - dx},{e.Y} {e.X},{e.Y}");
    }

    /// <summary>Tip of the arrowhead — same as the end point.</summary>
    public Point ArrowTip => EndPoint;

    public void Dispose()
    {
        Source.PropertyChanged -= OnEndpointChanged;
        Target.PropertyChanged -= OnEndpointChanged;
        if (Application.Current is { } app)
            app.ActualThemeVariantChanged -= OnThemeVariantChanged;
    }
}
