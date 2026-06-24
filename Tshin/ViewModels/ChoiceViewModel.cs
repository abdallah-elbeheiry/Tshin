using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Tshin.ViewModels;

public partial class ChoiceViewModel : ViewModelBase
{
    private readonly Action _onChanged;

    [ObservableProperty]
    private string _displayText;

    [ObservableProperty]
    private NodeViewModel? _target;

    public ChoiceViewModel(string displayText, NodeViewModel? target, Action onChanged)
    {
        _displayText = displayText;
        _target = target;
        _onChanged = onChanged;
    }

    partial void OnDisplayTextChanged(string value) => _onChanged();
}
