using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Tshin.Core.Models;

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

    /// <summary>
    /// The editable condition tree that gates this choice in play mode, or null when the
    /// choice is unconditional. Edited through the recursive condition VMs.
    /// </summary>
    [ObservableProperty]
    private ConditionNodeViewModel? _conditionRoot;

    /// <summary>
    /// Gets or sets the optional condition as a domain tree. Bridges the editing VM
    /// (<see cref="ConditionRoot"/>) to the player and persistence layers: the getter
    /// rebuilds a fresh domain tree; the setter reconstructs the editing VMs.
    /// </summary>
    public IConditionComponentNode? Condition
    {
        get => ConditionRoot?.BuildModel();
        set => ConditionRoot = value is null
            ? null
            : ConditionNodeViewModel.FromModel(value, AvailableEntities, _onChanged);
    }

    public bool HasCondition => ConditionRoot is not null;

    /// <summary>
    /// Gets or sets the behavior when <see cref="Condition"/> evaluates to <see langword="false"/>.
    /// </summary>
    [ObservableProperty]
    private ConditionFalseBehavior _conditionFalseBehavior = ConditionFalseBehavior.Close;

    /// <summary>Close/Hide options for the inspector picker; index 0 = Close, 1 = Hide.</summary>
    public ObservableCollection<string> FalseBehaviorOptions { get; } = new() { "Close", "Hide" };

    public int SelectedFalseBehaviorIndex
    {
        get => ConditionFalseBehavior == ConditionFalseBehavior.Hide ? 1 : 0;
        set => ConditionFalseBehavior = value == 1 ? ConditionFalseBehavior.Hide : ConditionFalseBehavior.Close;
    }

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

    partial void OnConditionRootChanged(ConditionNodeViewModel? value)
    {
        OnPropertyChanged(nameof(HasCondition));
        _onChanged();
    }

    partial void OnConditionFalseBehaviorChanged(ConditionFalseBehavior value)
    {
        OnPropertyChanged(nameof(SelectedFalseBehaviorIndex));
        _onChanged();
    }
}
