using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Tshin.ViewModels;

/// <summary>
/// An entity displayed on the canvas and listed in entity pickers for commands.
/// </summary>
public partial class EntityViewModel : ViewModelBase
{
    private readonly IEditorContext _context;
    private readonly Action _onChanged;

    [ObservableProperty]
    private string _id;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private double _x;

    [ObservableProperty]
    private double _y;

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Whether this entity is shown at runtime; toggled from the editor.</summary>
    [ObservableProperty]
    private bool _visible = true;

    // Layout position on the (biased) canvas — see NodeLayout.CanvasBias. Bound to
    // Canvas.Left/Top so the card renders inside the canvas's bounds at any world X/Y.
    public double CanvasX => X + NodeLayout.CanvasBias;
    public double CanvasY => Y + NodeLayout.CanvasBias;

    public ObservableCollection<ComponentViewModel> Components { get; } = new();

    public EntityViewModel(string id, string name, double x, double y, IEditorContext context)
    {
        _id = id;
        _name = name;
        _x = x;
        _y = y;
        _context = context;
        _onChanged = context.MarkDirty;
    }

    /// <summary>Deletes this entity from the graph.</summary>
    [RelayCommand]
    private void RemoveSelf() => _context.RemoveEntity(this);

    partial void OnNameChanged(string value) => _context.NoteContinuousChange(this, "text");

    partial void OnXChanged(double value)
    {
        OnPropertyChanged(nameof(CanvasX));
        _context.NoteContinuousChange(this, "pos");
    }

    partial void OnYChanged(double value)
    {
        OnPropertyChanged(nameof(CanvasY));
        _context.NoteContinuousChange(this, "pos");
    }

    // Selection is transient view state — it must not mark dirty or record history.
    partial void OnIsSelectedChanged(bool value) { }
    partial void OnVisibleChanged(bool value) => _onChanged();
}