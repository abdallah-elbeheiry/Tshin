using CommunityToolkit.Mvvm.ComponentModel;
using Tshin.Core.Models;

namespace Tshin.ViewModels;

public partial class ChoiceViewModel : ViewModelBase
{
    private readonly IChoice _choice;

    public ChoiceViewModel(IChoice choice, NodeViewModel? targetNode = null)
    {
        _choice = choice;
        _displayText = choice.DisplayText;
        _targetNode = targetNode;
    }

    [ObservableProperty]
    private string _displayText;

    [ObservableProperty]
    private NodeViewModel? _targetNode;

    partial void OnDisplayTextChanged(string value) => _choice.DisplayText = value;
    partial void OnTargetNodeChanged(NodeViewModel? value)
    {
        if (value != null) _choice.Node = value.Model;
    }
    
    public IChoice Model => _choice;
}
