using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Xunit;

namespace Tshin.Tests;

/// <summary>
/// The palette is split into Light and Dark theme dictionaries. Every brush key the chrome
/// and the wires reference must resolve in <em>both</em> variants, or a live OS theme switch
/// would leave holes. This locks that contract structurally (no color-value assertions).
/// </summary>
public class ThemeTests
{
    // Core chrome brushes referenced across the views + all seven wire brushes.
    public static TheoryData<string> BrushKeys()
    {
        var data = new TheoryData<string>
        {
            "WindowBgBrush", "SidebarBgBrush", "ContentBgBrush", "CanvasBgBrush",
            "CardBgBrush", "CardBgHoverBrush", "ControlBgBrush",
            "AccentBrush", "AccentHoverBrush", "EntityAccentBrush", "EntityHeaderBrush",
            "DangerBrush", "DangerHoverBrush", "DangerSubtleBrush",
            "HeaderOverlayBrush", "GhostBgBrush", "TitleHoverBrush", "TitleFocusBrush",
            "TextPrimaryBrush", "TextSecondaryBrush", "TextTertiaryBrush",
            "SeparatorBrush", "ControlBorderBrush",
        };
        for (var i = 0; i < ConnectionViewModelPaletteCount; i++)
            data.Add($"WireBrush{i}");
        return data;
    }

    // Kept in sync with ConnectionViewModel.PaletteCount without a project reference cost.
    private const int ConnectionViewModelPaletteCount = 7;

    [AvaloniaTheory]
    [MemberData(nameof(BrushKeys))]
    public void Brush_resolves_in_both_theme_variants(string key)
    {
        var app = Application.Current!;
        Assert.True(app.TryGetResource(key, ThemeVariant.Dark, out var dark) && dark is IBrush,
            $"'{key}' is missing from the Dark theme dictionary.");
        Assert.True(app.TryGetResource(key, ThemeVariant.Light, out var light) && light is IBrush,
            $"'{key}' is missing from the Light theme dictionary.");
    }
}
