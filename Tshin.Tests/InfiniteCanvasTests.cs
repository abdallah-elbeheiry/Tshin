using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Tshin.ViewModels;
using Tshin.Views;
using Xunit;

namespace Tshin.Tests;

/// <summary>
/// Bug 4: cards were culled when dragged far in any direction. Avalonia's Canvas
/// (the ItemsPanel) does not render children whose LAYOUT position falls outside their
/// own bounds — independent of ClipToBounds. The fix lays cards out at world+CanvasBias
/// inside a large fixed canvas (origin at the center) and cancels the bias in the render
/// transform. These tests lock the invariant that keeps every card inside the canvas
/// bounds, and that the on-screen mapping is unchanged.
/// </summary>
public class InfiniteCanvasTests
{
    private static void Pump() => Dispatcher.UIThread.RunJobs();

    [AvaloniaTheory]
    [InlineData(0, 0)]
    [InlineData(-90_000, -90_000)]   // dragged far up-and-left (negative)
    [InlineData(90_000, 90_000)]     // dragged far down-and-right
    [InlineData(-90_000, 90_000)]
    public void Card_at_extreme_coordinates_stays_inside_canvas_bounds(double x, double y)
    {
        var editor = TestFactory.Editor();
        var node = editor.CreateNodeAt(x, y);

        // The layout coordinate (bound to Canvas.Left/Top) must sit strictly inside the
        // canvas [0, CanvasSize]; that is exactly what keeps Avalonia from culling it.
        Assert.True(node.CanvasX is > 0 and < NodeLayout.CanvasSize,
            $"CanvasX {node.CanvasX} escaped [0,{NodeLayout.CanvasSize}] — card would be culled");
        Assert.True(node.CanvasY is > 0 and < NodeLayout.CanvasSize,
            $"CanvasY {node.CanvasY} escaped [0,{NodeLayout.CanvasSize}] — card would be culled");
    }

    [AvaloniaFact]
    public void Card_layout_position_is_bias_applied_and_reaches_the_visual_tree()
    {
        var editor = TestFactory.Editor();
        var node = editor.CreateNodeAt(-4000, -3000);

        var view = new EditorView { DataContext = editor };
        var window = new Window { Width = 1000, Height = 800, Content = view };
        window.Show();
        Pump();

        Assert.Equal(node.X + NodeLayout.CanvasBias, node.CanvasX);

        // The card's arranged position within the Canvas must equal its biased coord.
        var presenter = view.GetVisualDescendants().OfType<ContentPresenter>()
            .First(p => p.DataContext == node);
        Assert.Equal(node.CanvasX, Canvas.GetLeft(presenter));
        Assert.Equal(node.CanvasY, Canvas.GetTop(presenter));

        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(0, 1.0)]
    [InlineData(150, 0.25)]
    [InlineData(-500, 2.5)]
    public void Render_offset_cancels_the_layout_bias(double offset, double zoom)
    {
        var editor = TestFactory.Editor();
        editor.SetZoom(zoom);
        editor.OffsetX = offset;
        editor.OffsetY = offset;

        // The transform subtracts CanvasBias*Zoom so that, combined with the +CanvasBias
        // baked into every card's Canvas.Left, the on-screen mapping is the original
        // screen = world*Zoom + Offset (world origin unchanged).
        Assert.Equal(offset - NodeLayout.CanvasBias * zoom, editor.RenderOffsetX, 6);
        Assert.Equal(offset - NodeLayout.CanvasBias * zoom, editor.RenderOffsetY, 6);

        // Sanity: a card's biased layout, run through the render offset, lands where the
        // unbiased world→screen formula predicts.
        var node = editor.CreateNodeAt(1234, -567);
        Assert.Equal(node.X * zoom + offset, node.CanvasX * zoom + editor.RenderOffsetX, 4);
        Assert.Equal(node.Y * zoom + offset, node.CanvasY * zoom + editor.RenderOffsetY, 4);
    }

    [AvaloniaFact]
    public void Canvas_items_controls_do_not_clip()
    {
        var editor = TestFactory.Editor();
        var view = new EditorView { DataContext = editor };
        var window = new Window { Width = 1000, Height = 800, Content = view };
        window.Show();
        Pump();

        var world = view.GetVisualDescendants().OfType<Panel>().First(p => p.Name == "World");
        var canvasLists = world.GetVisualDescendants().OfType<ItemsControl>().ToList();

        Assert.NotEmpty(canvasLists);
        Assert.All(canvasLists, ic => Assert.False(ic.ClipToBounds));
    }
}
