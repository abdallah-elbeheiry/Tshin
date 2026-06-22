using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tshin.Core.Models;
using Tshin.Core.Utils;
using System.Linq;

namespace Tshin.ViewModels;

public partial class PlayerViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainViewModel;

    [ObservableProperty]
    private NodeViewModel? _currentNode;

    public PlayerViewModel(MainWindowViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        
        var coreNodes = NodeManager.GetNodes().ToList();
        var allNodeVms = coreNodes.Select(n => new NodeViewModel(n)).ToList();
        
        foreach (var nodeVm in allNodeVms)
        {
            nodeVm.InitializeChoices(new System.Collections.ObjectModel.ObservableCollection<NodeViewModel>(allNodeVms));
        }

        CurrentNode = allNodeVms.FirstOrDefault();
    }

    [RelayCommand]
    private void SelectChoice(ChoiceViewModel choice)
    {
        if (choice.TargetNode != null)
        {
            CurrentNode = choice.TargetNode;
        }
    }

    [RelayCommand]
    private void BackToMenu()
    {
        _mainViewModel.NavigateToMainMenu();
    }
}
