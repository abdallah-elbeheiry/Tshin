using Avalonia;
using Avalonia.Headless;
using Tshin;
using Tshin.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace Tshin.Tests;

/// <summary>
/// Boots the real <see cref="App"/> (styles, resources, ViewLocator) on Avalonia's
/// headless platform so tests can construct view models and render actual windows
/// without a display. Referenced by <c>[AvaloniaTestApplication]</c> above.
/// </summary>
public sealed class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
