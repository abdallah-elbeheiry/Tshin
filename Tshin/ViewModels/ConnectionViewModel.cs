using System;
using System.Collections.Generic;
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
    private static readonly IReadOnlyList<IBrush> WirePalette;
    private static int _nextColorIndex;

    static ConnectionViewModel()
    {
        var keys = new[] { "WireBrush0", "WireBrush1", "WireBrush2", "WireBrush3",
                           "WireBrush4", "WireBrush5", "WireBrush6" };
        var list = new List<IBrush>();
        foreach (var t in keys)
        {
            IBrush? brush = null;
            if (Application.Current != null &&
                Application.Current.TryGetResource(t, null, out var res) &&
                res is IBrush b)
            {
                brush = b;
            }

            if (brush != null) list.Add(brush);
        }
        WirePalette = list.AsReadOnly();
    }

    public NodeViewModel Source { get; }
    public NodeViewModel Target { get; }
    public int ChoiceIndex { get; }

    public IBrush Stroke { get; }

    public ConnectionViewModel(NodeViewModel source, NodeViewModel target, int choiceIndex)
    {
        Source = source;
        Target = target;
        ChoiceIndex = choiceIndex;
        Stroke = WirePalette[_nextColorIndex++ % WirePalette.Count];
        Source.PropertyChanged += OnEndpointChanged;
        Target.PropertyChanged += OnEndpointChanged;
    }

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
    }
}
