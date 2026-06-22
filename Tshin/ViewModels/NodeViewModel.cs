using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Tshin.Core.Models;
using Tshin.Core.Utils;

namespace Tshin.ViewModels;

public partial class NodeViewModel : ViewModelBase
{
    private readonly INode _node;

    public NodeViewModel(INode node)
    {
        _node = node;
        _id = node.Id;
        _displayText = node.DisplayText;
        Choices = new ObservableCollection<ChoiceViewModel>();
    }

    public void InitializeChoices(ObservableCollection<NodeViewModel> allNodes)
    {
        Choices.Clear();
        if (_node is IBranchingNode branchingNode)
        {
            foreach (var choice in branchingNode.Choices)
            {
                var targetVm = allNodes.FirstOrDefault(n => n.Model == choice.Node);
                Choices.Add(new ChoiceViewModel(choice, targetVm));
            }
        }
    }

    [ObservableProperty]
    private string _id;

    [ObservableProperty]
    private string _displayText;

    public ObservableCollection<ChoiceViewModel> Choices { get; }

    partial void OnIdChanged(string value)
    {
        NodeManager.ModifyNodeId(_node.Id, value);
    }

    partial void OnDisplayTextChanged(string value)
    {
        _node.DisplayText = value;
    }

    public void AddChoice(NodeViewModel targetNodeVm)
    {
        if (_node is IBranchingNode branchingNode)
        {
            var choice = new Tshin.Core.Models.StoryChoice(targetNodeVm.Model, "New Choice");
            NodeManager.AddChoice(choice, branchingNode);
            Choices.Add(new ChoiceViewModel(choice, targetNodeVm));
        }
    }

    public NodeViewModel? TargetNodeProxy
    {
        get => null;
        set { if (value != null) AddChoice(value); }
    }

    public void RemoveChoice(ChoiceViewModel choiceVm)
    {
        if (_node is IBranchingNode branchingNode)
        {
            NodeManager.RemoveChoice(choiceVm.Model, branchingNode);
            Choices.Remove(choiceVm);
        }
    }

    public INode Model => _node;
}
