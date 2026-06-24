using System;
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace Tshin.ViewModels;

/// <summary>
/// A bezier wire from one choice's output pin to a target node's input pin.
/// Recomputes its geometry whenever either endpoint node moves.
/// </summary>
public sealed class ConnectionViewModel : ViewModelBase, IDisposable
{
    public NodeViewModel Source { get; }
    public NodeViewModel Target { get; }
    public int ChoiceIndex { get; }

    public ConnectionViewModel(NodeViewModel source, NodeViewModel target, int choiceIndex)
    {
        Source = source;
        Target = target;
        ChoiceIndex = choiceIndex;
        Source.PropertyChanged += OnEndpointChanged;
        Target.PropertyChanged += OnEndpointChanged;
    }

    private void OnEndpointChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(NodeViewModel.X) or nameof(NodeViewModel.Y))
        {
            OnPropertyChanged(nameof(StartPoint));
            OnPropertyChanged(nameof(EndPoint));
            OnPropertyChanged(nameof(PathData));
            OnPropertyChanged(nameof(Geometry));
        }
    }

    public Point StartPoint => new(NodeLayout.OutputPinX(Source), NodeLayout.OutputPinY(Source, ChoiceIndex));
    public Point EndPoint => new(NodeLayout.InputPinX(Target), NodeLayout.InputPinY(Target));

    public string PathData => BuildPath(StartPoint, EndPoint);

    public Geometry Geometry => Geometry.Parse(PathData);

    /// <summary>Cubic bezier path string (invariant culture, parseable by Path.Data).</summary>
    public static string BuildPath(Point s, Point e)
    {
        var dx = Math.Max(40, Math.Abs(e.X - s.X) * 0.5);
        return string.Create(CultureInfo.InvariantCulture,
            $"M {s.X},{s.Y} C {s.X + dx},{s.Y} {e.X - dx},{e.Y} {e.X},{e.Y}");
    }

    public void Dispose()
    {
        Source.PropertyChanged -= OnEndpointChanged;
        Target.PropertyChanged -= OnEndpointChanged;
    }
}
