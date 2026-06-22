using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tshin.Core.Models;
using Tshin.Core.Utils;

namespace Tshin.ViewModels;

public partial class EditorViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainViewModel;
    
    [ObservableProperty]
    private bool _isDirty;

    public EditorViewModel(MainWindowViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        Nodes = new ObservableCollection<NodeViewModel>();
        
        foreach (var node in NodeManager.GetNodes())
        {
            Nodes.Add(new NodeViewModel(node, SetDirty));
        }
        
        foreach (var nodeVm in Nodes)
        {
            nodeVm.InitializeChoices(Nodes);
        }

        IsDirty = false;
    }

    private void SetDirty() => IsDirty = true;

    public ObservableCollection<NodeViewModel> Nodes { get; }

    [RelayCommand]
    private void AddNode()
    {
        var node = NodeFactory.CreateNode(NodeType.StoryNode);
        var vm = new NodeViewModel(node, SetDirty);
        Nodes.Add(vm);
        IsDirty = true;
    }

    [RelayCommand]
    private void RemoveNode(NodeViewModel nodeVm)
    {
        NodeManager.RemoveNode(nodeVm.Model);
        Nodes.Remove(nodeVm);
        IsDirty = true;
    }

    [RelayCommand]
    private void AddChoice(NodeViewModel nodeVm)
    {
        if (Nodes.Any())
        {
            nodeVm.AddChoice(Nodes.First());
            IsDirty = true;
        }
    }

    [RelayCommand]
    private void RemoveChoice(ChoiceViewModel choiceVm)
    {
        var nodeVm = Nodes.FirstOrDefault(n => n.Choices.Contains(choiceVm));
        nodeVm?.RemoveChoice(choiceVm);
        IsDirty = true;
    }

    [ObservableProperty]
    private bool _showUnsavedChangesDialog;

    [RelayCommand]
    private void Save()
    {
        _mainViewModel.TriggerSave();
        IsDirty = false;
        ShowUnsavedChangesDialog = false;
    }

    [RelayCommand]
    private void Back()
    {
        if (IsDirty)
        {
            ShowUnsavedChangesDialog = true;
        }
        else
        {
            _mainViewModel.NavigateBackFromEditor();
        }
    }

    [RelayCommand]
    private void ExitWithoutSaving()
    {
        IsDirty = false;
        ShowUnsavedChangesDialog = false;
        _mainViewModel.NavigateBackFromEditor();
    }

    [RelayCommand]
    private void CancelBack()
    {
        ShowUnsavedChangesDialog = false;
    }
}
