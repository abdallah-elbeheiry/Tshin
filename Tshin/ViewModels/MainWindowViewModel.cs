using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Tshin.Core.Utils;

namespace Tshin.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _currentPage;

    public MainWindowViewModel()
    {
        _currentPage = new MainMenuViewModel(this);
    }

    public void NavigateToMainMenu()
    {
        CurrentPage = new MainMenuViewModel(this);
    }

    public void NavigateToCreateMenu()
    {
        CurrentPage = new CreateMenuViewModel(this);
    }

    public void StartNewEpic()
    {
        NodeManager.ClearNodes();
        CurrentPage = new EditorViewModel(this);
    }

    public void RequestExit()
    {
        // This would typically involve an event or a service to close the window
        // For simplicity, we can use Environment.Exit for now or just let the user close it
        Environment.Exit(0);
    }

    public async void TriggerLoadForPlay()
    {
        string filePath = await RequestFilePath();
        if (!string.IsNullOrEmpty(filePath))
        {
            if (await LoadFromFileAsync(filePath))
            {
                CurrentPage = new PlayerViewModel(this);
            }
        }
    }

    public async void TriggerLoadForEdit()
    {
        string filePath = await RequestFilePath();
        if (!string.IsNullOrEmpty(filePath))
        {
            if (await LoadFromFileAsync(filePath))
            {
                CurrentPage = new EditorViewModel(this);
            }
        }
    }

    public async void TriggerSave()
    {
        // We'll need a way to know where to save
        // For now, let's just trigger the file picker
        // Ideally we store the current file path
        string filePath = await RequestSavePath();
        if (!string.IsNullOrEmpty(filePath))
        {
            await SaveToFileAsync(filePath);
        }
    }

    public void NavigateBackFromEditor()
    {
        if (CurrentPage is EditorViewModel { IsDirty: true })
        {
            // We should show a dialog, but for now let's just use a simple flag or assume we need to ask
            // Since we can't easily do a blocking dialog from VM without a service, 
            // I'll implement a simple "NeedsSave" state or just navigate for now and come back to it.
            // Requirement says: "tells you that the file is unsaved do you want to save?"
            // I'll add a boolean to show a confirmation overlay in the view.
        }
        NavigateToMainMenu();
    }

    // Communication with View for file picking
    public Func<Task<string>>? FilePickerRequest { get; set; }
    public Func<Task<string>>? FileSaveRequest { get; set; }

    private async Task<string> RequestFilePath() => FilePickerRequest != null ? await FilePickerRequest() : string.Empty;
    private async Task<string> RequestSavePath() => FileSaveRequest != null ? await FileSaveRequest() : string.Empty;

    public async Task SaveToFileAsync(string filePath)
    {
        await FileWriter.SaveFileAsync(filePath);
    }

    public async Task<bool> LoadFromFileAsync(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return false;

        try
        {
            await FileReader.LoadFileAsync(filePath);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading file: {ex.Message}");
            return false;
        }
    }
}