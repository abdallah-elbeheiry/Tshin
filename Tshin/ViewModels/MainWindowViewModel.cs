using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Tshin.Core.Models;
using Tshin.Core.Utils;

namespace Tshin.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel()
    {
        Nodes = new ObservableCollection<NodeViewModel>();
        var coreNodes = NodeManager.GetNodes().ToList();
        
        // First pass: create all NodeViewModels without choices linked yet
        foreach (var node in coreNodes)
        {
            Nodes.Add(new NodeViewModel(node));
        }
        
        // Second pass: initialize choices now that all NodeViewModels exist
        foreach (var nodeVm in Nodes)
        {
            nodeVm.InitializeChoices(Nodes);
        }
    }

    public ObservableCollection<NodeViewModel> Nodes { get; }

    [RelayCommand]
    private void AddNode()
    {
        var node = NodeFactory.CreateNode(NodeType.StoryNode);
        Nodes.Add(new NodeViewModel(node));
    }

    [RelayCommand]
    private void RemoveNode(NodeViewModel nodeVm)
    {
        NodeManager.RemoveNode(nodeVm.Model);
        Nodes.Remove(nodeVm);
    }

    [RelayCommand]
    private void AddChoice(NodeViewModel nodeVm)
    {
        if (Nodes.Any())
        {
            // By default link to the first node
            nodeVm.AddChoice(Nodes.First());
        }
    }

    [RelayCommand]
    private void RemoveChoice(ChoiceViewModel choiceVm)
    {
        var nodeVm = Nodes.FirstOrDefault(n => n.Choices.Contains(choiceVm));
        nodeVm?.RemoveChoice(choiceVm);
    }

    public async Task SaveToFileAsync(string filePath)
    {
        await FileWriter.SaveFileAsync(filePath);
    }
}