using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Tshin.ViewModels;

public partial class ChoiceViewModel : ViewModelBase
{
    private readonly Action _onChanged;

    [ObservableProperty]
    private string _displayText;

    [ObservableProperty]
    private NodeViewModel? _target;

    [ObservableProperty]
    private bool _isSelected;

    public bool IsValid => Target is not null;

    public ObservableCollection<CommandViewModel> Commands { get; } = new();

    /// <summary>
    /// Reference to the editor's entities collection, shared so command entity pickers
    /// stay in sync. Set by the editor when a choice is created.
    /// </summary>
    public ObservableCollection<EntityViewModel>? AvailableEntities { get; set; }

    public ChoiceViewModel(string displayText, NodeViewModel? target, Action onChanged)
    {
        _displayText = displayText;
        _target = target;
        _onChanged = onChanged;
    }

    partial void OnDisplayTextChanged(string value) => _onChanged();
    partial void OnIsSelectedChanged(bool value) => _onChanged();
}