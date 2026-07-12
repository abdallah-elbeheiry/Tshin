using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;

namespace Tshin.Platform;

/// <summary>
/// Per-platform window chrome. macOS keeps an extended client area (a custom drag strip and a
/// traffic-light inset); every other platform uses native decorations so real minimise /
/// maximise / close buttons appear. Acrylic is feature-detected at runtime and falls back to a
/// flat surface where the compositor doesn't grant a blur transparency level.
/// </summary>
public static class PlatformChrome
{
    public static bool IsMac { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    /// <summary>Top inset reserved for the macOS traffic lights; zero on other platforms.</summary>
    public static Thickness TitleBarInset { get; } = IsMac ? new Thickness(0, 30, 0, 0) : default;

    /// <summary>
    /// Applies native decorations off macOS. The macOS extended-client-area setup already
    /// lives in XAML, so this is a no-op there.
    /// </summary>
    public static void ApplyWindowChrome(Window window)
    {
        if (IsMac) return;

        window.ExtendClientAreaToDecorationsHint = false;
        window.ExtendClientAreaTitleBarHeightHint = 0;
    }

    /// <summary>
    /// True when the compositor actually granted a blur transparency level; call after the
    /// window is open. When false, callers should hide their acrylic layer and show a flat one.
    /// </summary>
    public static bool AcrylicGranted(Window window)
    {
        var level = window.ActualTransparencyLevel;
        return level == WindowTransparencyLevel.AcrylicBlur
            || level == WindowTransparencyLevel.Blur
            || level == WindowTransparencyLevel.Mica;
    }
}
