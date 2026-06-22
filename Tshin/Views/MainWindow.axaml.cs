using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Tshin.ViewModels;

namespace Tshin.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public async void SaveButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            var topLevel = GetTopLevel(this);
            if (topLevel == null) return;

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

            if (file is not null)
            {
                await vm.SaveToFileAsync(file.Path.LocalPath);
            }
        }
    }
}