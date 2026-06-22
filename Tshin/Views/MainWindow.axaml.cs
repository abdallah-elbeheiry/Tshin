using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Tshin.ViewModels;

namespace Tshin.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (sender, args) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.FilePickerRequest = ShowOpenFilePickerAsync;
                vm.FileSaveRequest = ShowSaveFilePickerAsync;
            }
        };
    }

    private async Task<string> ShowOpenFilePickerAsync()
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return string.Empty;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Tshin File",
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Tshin Files")
                {
                    Patterns = new[] { "*.tshin" }
                }
            },
            AllowMultiple = false
        });

        return files.Count >= 1 ? files[0].Path.LocalPath : string.Empty;
    }

    private async Task<string> ShowSaveFilePickerAsync()
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return string.Empty;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Tshin File",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Tshin Files")
                {
                    Patterns = new[] { "*.tshin" }
                }
            },
            DefaultExtension = "tshin",
            SuggestedFileName = "story.tshin"
        });

        return file?.Path.LocalPath ?? string.Empty;
    }
}