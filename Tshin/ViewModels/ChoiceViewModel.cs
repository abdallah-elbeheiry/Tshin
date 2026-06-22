using CommunityToolkit.Mvvm.ComponentModel;
using Tshin.Core.Models;

namespace Tshin.ViewModels;

public partial class ChoiceViewModel : ViewModelBase
{
    private readonly IChoice _choice;
    private readonly System.Action? _onChanged;
    private NodeViewModel? _targetNode;

    public ChoiceViewModel(IChoice choice, NodeViewModel? targetNode = null, System.Action? onChanged = null)
    {
        _choice = choice;
        _targetNode = targetNode;
        _onChanged = onChanged;
    }

    public string DisplayText
    {
        get => _choice.DisplayText;
        set
        {
            if (_choice.DisplayText == value) return;
            _choice.DisplayText = value;
            OnPropertyChanged(nameof(DisplayText));
            _onChanged?.Invoke();
        }
    }

    public NodeViewModel? TargetNode
    {
        get => _targetNode;
        set
        {
            if (_targetNode == value) return;
            _targetNode = value;
            _choice.Node = value?.Model;
            OnPropertyChanged(nameof(TargetNode));
            _onChanged?.Invoke();
        }
    }

    public IChoice Model => _choice;
}
