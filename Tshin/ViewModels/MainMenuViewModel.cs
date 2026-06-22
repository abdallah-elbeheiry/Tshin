using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Tshin.ViewModels;

public partial class MainMenuViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainViewModel;

    public MainMenuViewModel(MainWindowViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
    }

    [RelayCommand]
    private void Play()
    {
        _mainViewModel.TriggerLoadForPlay();
    }

    [RelayCommand]
    private void Create()
    {
        _mainViewModel.NavigateToCreateMenu();
    }

    [RelayCommand]
    private void Exit()
    {
        _mainViewModel.RequestExit();
    }
}
