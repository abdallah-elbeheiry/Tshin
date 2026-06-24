using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Tshin.ViewModels;

public partial class NodeViewModel : ViewModelBase
{
    private readonly Action _onChanged;

    [ObservableProperty]
    private string _id;

    [ObservableProperty]
    private string _displayText;

    [ObservableProperty]
    private double _x;

    [ObservableProperty]
    private double _y;

    [ObservableProperty]
    private bool _isSelected;

    public ObservableCollection<ChoiceViewModel> Choices { get; } = new();

    public NodeViewModel(string id, string displayText, double x, double y, Action onChanged)
    {
        _id = id;
        _displayText = displayText;
        _x = x;
        _y = y;
        _onChanged = onChanged;
    }

    partial void OnIdChanged(string value) => _onChanged();
    partial void OnDisplayTextChanged(string value) => _onChanged();
    // X/Y changes happen during drags; mark dirty so the move can be saved.
    partial void OnXChanged(double value) => _onChanged();
    partial void OnYChanged(double value) => _onChanged();

    public ChoiceViewModel AddChoice(NodeViewModel? target = null)
    {
        var choice = new ChoiceViewModel("New choice", target, _onChanged);
        Choices.Add(choice);
        _onChanged();
        return choice;
    }
}
