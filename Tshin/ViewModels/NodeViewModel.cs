using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Tshin.Core.Models;
using Tshin.Core.Utils;

namespace Tshin.ViewModels;

public partial class NodeViewModel : ViewModelBase
{
    public NodeViewModel(INode node, System.Action? onChanged = null)
    {
        Model = node;
        _onChanged = onChanged;
        Choices = [];
    }

    private readonly System.Action? _onChanged;

    public string Id
    {
        get => Model.Id;
        set
        {
            if (Model.Id == value) return;
            var oldId = Model.Id;
            NodeManager.ModifyNodeId(oldId, value);
            OnPropertyChanged(nameof(Id));
            _onChanged?.Invoke();
        }
    }

    public string DisplayText
    {
        get => Model.DisplayText;
        set
        {
            if (Model.DisplayText == value) return;
            Model.DisplayText = value;
            OnPropertyChanged(nameof(DisplayText));
            _onChanged?.Invoke();
        }
    }

    public ObservableCollection<ChoiceViewModel> Choices { get; }

    public void InitializeChoices(ObservableCollection<NodeViewModel> allNodes)
    {
        Choices.Clear();
        if (Model is IBranchingNode branchingNode)
        {
            foreach (var choice in branchingNode.Choices)
            {
                var targetVm = allNodes.FirstOrDefault(n => n.Model == choice.Node);
                Choices.Add(new ChoiceViewModel(choice, targetVm, _onChanged));
            }
        }
    }

    public void AddChoice(NodeViewModel targetNodeVm)
    {
        if (Model is IBranchingNode branchingNode)
        {
            var choice = new StoryChoice(targetNodeVm.Model, "New Choice");
            NodeManager.AddChoice(choice, branchingNode);
            Choices.Add(new ChoiceViewModel(choice, targetNodeVm, _onChanged));
            _onChanged?.Invoke();
        }
    }

    public NodeViewModel? TargetNodeProxy
    {
        get => null;
        set { if (value != null) AddChoice(value); }
    }

    public void RemoveChoice(ChoiceViewModel choiceVm)
    {
        if (Model is not IBranchingNode branchingNode) return;
        NodeManager.RemoveChoice(choiceVm.Model, branchingNode);
        Choices.Remove(choiceVm);
        _onChanged?.Invoke();
    }

    public INode Model { get; }
}
