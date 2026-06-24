using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Tshin.ViewModels;

/// <summary>
/// A play-through of the current graph (Run mode). Walks the live
/// <see cref="NodeViewModel"/> graph, so unsaved edits are playable immediately.
/// </summary>
public partial class PlayerViewModel : ViewModelBase
{
    private readonly NodeViewModel? _start;

    [ObservableProperty]
    private NodeViewModel? _current;

    public PlayerViewModel(NodeViewModel? start)
    {
        _start = start;
        _current = start;
    }

    public bool IsEnd => Current is null || Current.Choices.Count == 0;

    partial void OnCurrentChanged(NodeViewModel? value) => OnPropertyChanged(nameof(IsEnd));

    [RelayCommand]
    private void Choose(ChoiceViewModel? choice)
    {
        if (choice?.Target is { } target)
            Current = target;
    }

    [RelayCommand]
    private void Restart() => Current = _start;
}
