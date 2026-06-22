using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Tshin.ViewModels;

public partial class CreateMenuViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainViewModel;

    public CreateMenuViewModel(MainWindowViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
    }

    [RelayCommand]
    private void NewEpic()
    {
        _mainViewModel.StartNewEpic();
    }

    [RelayCommand]
    private void EditExisting()
    {
        _mainViewModel.TriggerLoadForEdit();
    }

    [RelayCommand]
    private void Back()
    {
        _mainViewModel.NavigateToMainMenu();
    }
}
