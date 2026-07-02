using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Tshin.ViewModels;

/// <summary>
/// An entity displayed on the canvas and listed in entity pickers for commands.
/// </summary>
public partial class EntityViewModel : ViewModelBase
{
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

    public ObservableCollection<ComponentViewModel> Components { get; } = new();

    public EntityViewModel(string id, string name, double x, double y, Action onChanged)
    {
        _id = id;
        _name = name;
        _x = x;
        _y = y;
        _onChanged = onChanged;
    }

    partial void OnNameChanged(string value) => _onChanged();
    partial void OnXChanged(double value) => _onChanged();
    partial void OnYChanged(double value) => _onChanged();
    partial void OnIsSelectedChanged(bool value) => _onChanged();
}