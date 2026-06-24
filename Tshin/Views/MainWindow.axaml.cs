using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Tshin.ViewModels;

namespace Tshin.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
                vm.ImportFileRequest = ShowImportFilePickerAsync;
        };

        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (!e.DataTransfer.Contains(DataFormat.File)) return;

        var files = e.DataTransfer.TryGetFiles();
        if (files is null) return;

        foreach (var item in files.OfType<IStorageFile>())
        {
            var path = item.Path.LocalPath;
            if (!string.IsNullOrEmpty(path))
                await vm.ImportFromPathAsync(path);
        }
    }

    private async Task<string?> ShowImportFilePickerAsync()
    {
        var topLevel = GetTopLevel(this);
        if (topLevel is null) return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Tshin Project",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Tshin Files") { Patterns = ["*.tshin"] },
                new FilePickerFileType("All Files") { Patterns = ["*"] },
            ],
        });

        return files.Count >= 1 ? files[0].Path.LocalPath : null;
    }
}
