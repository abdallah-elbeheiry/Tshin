using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System;
using System.IO;
using System.Linq;
using Avalonia.Markup.Xaml;
using Tshin.Platform;
using Tshin.Services;
using Tshin.ViewModels;
using Tshin.Views;

namespace Tshin;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Replace the palette's default title-bar inset with the platform-computed one
        // (a traffic-light gap on macOS, zero elsewhere) so off-Mac chrome reserves no dead
        // space. DynamicResource consumers pick this up.
        Resources["TitleBarInset"] = PlatformChrome.TitleBarInset;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var storageRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tshin");

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(new FileProjectService(storageRoot)),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
