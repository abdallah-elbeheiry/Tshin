using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Tshin.ViewModels;

namespace Tshin.Views;

/// <summary>
/// The always-dark node canvas: the pan/zoom viewport, the biased world panel that hosts the
/// wires and node/entity cards, and the pointer state machine that drives panning, dragging,
/// and connecting. It inherits its <see cref="EditorViewModel"/> DataContext from the parent
/// <see cref="EditorView"/>. The toolbar, inspector, and empty state live in EditorView.
/// </summary>
public partial class CanvasView : UserControl
{
    private enum Mode { None, Pan, Node, Connect, Entity }

    private Mode _mode;
    private Point _last;
    private NodeViewModel? _dragNode;
    private ChoiceViewModel? _connectChoice;
    private NodeViewModel? _connectOwner;
    private EntityViewModel? _dragEntity;
    private int _connectIndex = -1;
    private Point _lastRightClickPosition;

    private EditorViewModel? _vm;

    private const double GridCell = 26;
    private static readonly Color GridDotColor = Color.Parse("#3A3A3E");
    private static readonly Color CanvasBgColor = Color.Parse("#161618"); // matches CanvasBgBrush

    public CanvasView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        UpdateGridBackground();
    }

    private EditorViewModel? Vm => DataContext as EditorViewModel;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
        {
            _vm.RequestFit -= FitToView;
            _vm.PropertyChanged -= OnVmPropertyChanged;
        }
        _vm = Vm;
        if (_vm is not null)
        {
            _vm.RequestFit += FitToView;
            _vm.PropertyChanged += OnVmPropertyChanged;
        }
        UpdateGridBackground();
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Keep the grid aligned when zoom/offset change via toolbar buttons or code.
        if (e.PropertyName is nameof(EditorViewModel.Zoom)
            or nameof(EditorViewModel.OffsetX)
            or nameof(EditorViewModel.OffsetY))
        {
            UpdateGridBackground();
        }
    }

    /// <summary>
    /// Paints the dot grid on the viewport in screen space, following the current
    /// pan/zoom. Because the tile is re-projected from the world transform, the grid
    /// covers the whole viewport at any offset — an effectively infinite canvas.
    /// </summary>
    private void UpdateGridBackground()
    {
        double zoom = Vm?.Zoom ?? 1;
        double offsetX = Vm?.OffsetX ?? 0;
        double offsetY = Vm?.OffsetY ?? 0;

        var screenCell = GridCell * zoom;
        if (screenCell <= 0) return;

        // Normalise the pan offset into a single-cell phase so the grid scrolls smoothly.
        var phaseX = ((offsetX % screenCell) + screenCell) % screenCell;
        var phaseY = ((offsetY % screenCell) + screenCell) % screenCell;

        var dot = new Ellipse
        {
            Width = 2,
            Height = 2,
            Fill = new SolidColorBrush(GridDotColor),
        };
        Canvas.SetLeft(dot, screenCell / 2);
        Canvas.SetTop(dot, screenCell / 2);

        // Opaque tile (canvas colour + dot) so the brush fully paints the viewport.
        var tile = new Canvas
        {
            Width = screenCell,
            Height = screenCell,
            Background = new SolidColorBrush(CanvasBgColor),
            Children = { dot },
        };

        Viewport.Background = new VisualBrush
        {
            Visual = tile,
            TileMode = TileMode.Tile,
            Stretch = Stretch.None,
            SourceRect = new RelativeRect(0, 0, screenCell, screenCell, RelativeUnit.Absolute),
            DestinationRect = new RelativeRect(phaseX, phaseY, screenCell, screenCell, RelativeUnit.Absolute),
        };
    }

    private Point ToWorld(Point p)
        => Vm is { } vm ? new Point((p.X - vm.OffsetX) / vm.Zoom, (p.Y - vm.OffsetY) / vm.Zoom) : p;

    // ---- viewport (pan / zoom / background) --------------------------------

    private void OnViewportPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Vm is null) return;
        Focus();
        Vm.SelectNode(null);

        var pos = e.GetPosition(Viewport);
        _lastRightClickPosition = pos;

        if (e.GetCurrentPoint(Viewport).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed)
        {
            _mode = Mode.Pan;
            _last = pos;
            e.Pointer.Capture(Viewport);
        }
    }

    private void OnCreateNodeClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm) return;
        var world = ToWorld(_lastRightClickPosition);
        vm.CreateNodeAt(world.X - NodeLayout.Width / 2, world.Y);
    }

    private void OnCreateEntityClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm) return;
        var world = ToWorld(_lastRightClickPosition);
        vm.CreateEntityAt(world.X - NodeLayout.EntityWidth / 2, world.Y);
    }

    private void OnViewportMoved(object? sender, PointerEventArgs e)
    {
        if (Vm is not { } vm) return;
        var pos = e.GetPosition(Viewport);
        var d = pos - _last;

        switch (_mode)
        {
            case Mode.Pan:
                vm.OffsetX += d.X;
                vm.OffsetY += d.Y;
                _last = pos;
                break;
            case Mode.Node when _dragNode is not null:
                _dragNode.X += d.X / vm.Zoom;
                _dragNode.Y += d.Y / vm.Zoom;
                _last = pos;
                break;
            case Mode.Entity when _dragEntity is not null:
                _dragEntity.X += d.X / vm.Zoom;
                _dragEntity.Y += d.Y / vm.Zoom;
                _last = pos;
                break;
            case Mode.Connect:
                UpdateTempWire(pos);
                break;
        }
    }

    private void OnViewportReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (Vm is { } vm && _mode == Mode.Connect && _connectChoice is not null)
        {
            var world = ToWorld(e.GetPosition(Viewport));
            var target = NodeAt(world, vm);
            if (target is not null && target != _connectOwner)
                vm.Connect(_connectChoice, target);
            else if (target is null)
                vm.Disconnect(_connectChoice); // dropped on empty canvas → unlink
        }
        else if (Vm is { } vmNode && _mode == Mode.Node && _dragNode is not null)
        {
            _dragNode.X = vmNode.Snap(_dragNode.X);
            _dragNode.Y = vmNode.Snap(_dragNode.Y);
        }
        else if (Vm is { } vmEntity && _mode == Mode.Entity && _dragEntity is not null)
        {
            _dragEntity.X = vmEntity.Snap(_dragEntity.X);
            _dragEntity.Y = vmEntity.Snap(_dragEntity.Y);
        }

        TempWire.IsVisible = false;
        _mode = Mode.None;
        _dragNode = null;
        _dragEntity = null;
        _connectChoice = null;
        _connectOwner = null;
        _connectIndex = -1;
        e.Pointer.Capture(null);

        // Close the drag (or any open continuous run) as a single undo step.
        Vm?.FlushHistory();
    }

    private void OnViewportWheel(object? sender, PointerWheelEventArgs e)
    {
        if (Vm is not { } vm) return;
        var pos = e.GetPosition(Viewport);
        var worldBefore = ToWorld(pos);

        vm.SetZoom(vm.Zoom * (e.Delta.Y > 0 ? 1.1 : 1 / 1.1));

        // Keep the world point under the cursor pinned in place.
        vm.OffsetX = pos.X - worldBefore.X * vm.Zoom;
        vm.OffsetY = pos.Y - worldBefore.Y * vm.Zoom;
        e.Handled = true;
    }

    // ---- node dragging ------------------------------------------------------

    private void OnNodeHeaderPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: NodeViewModel node } && Vm is { } vm)
        {
            vm.SelectNode(node);
            _mode = Mode.Node;
            _dragNode = node;
            _last = e.GetPosition(Viewport);
            e.Pointer.Capture(Viewport);
            e.Handled = true;
        }
    }

    // ---- entity dragging ----------------------------------------------------

    private void OnEntityHeaderPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: EntityViewModel entity } && Vm is { } vm)
        {
            vm.SelectEntity(entity);
            _mode = Mode.Entity;
            _dragEntity = entity;
            _last = e.GetPosition(Viewport);
            e.Pointer.Capture(Viewport);
            e.Handled = true;
        }
    }

    private void OnComponentBadgePressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: ComponentViewModel component } && Vm is { } vm)
        {
            vm.SelectComponent(component);
            e.Handled = true;
        }
    }

    // ---- add component to entity -------------------------------------------

    private void OnAddComponentClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: EntityViewModel entity } && Vm is { } vm)
        {
            var menu = new ContextMenu();
            var numberItem = new MenuItem { Header = "Number" };
            numberItem.Click += (_, _) => vm.AddComponentToEntity(entity, "number");
            var textItem = new MenuItem { Header = "Text" };
            textItem.Click += (_, _) => vm.AddComponentToEntity(entity, "text");
            var conditionItem = new MenuItem { Header = "Condition" };
            conditionItem.Click += (_, _) => vm.AddComponentToEntity(entity, "condition");
            menu.Items.Add(numberItem);
            menu.Items.Add(textItem);
            menu.Items.Add(conditionItem);

            menu.Open((Control)sender);
        }
    }

    // ---- connecting ---------------------------------------------------------

    private void OnOutputPinPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: ChoiceViewModel choice } && Vm is { } vm)
        {
            _connectOwner = vm.Nodes.FirstOrDefault(n => n.Choices.Contains(choice));
            _connectIndex = _connectOwner?.Choices.IndexOf(choice) ?? -1;
            if (_connectOwner is null || _connectIndex < 0) return;

            _connectChoice = choice;
            _mode = Mode.Connect;
            TempWire.IsVisible = true;
            UpdateTempWire(e.GetPosition(Viewport));
            e.Pointer.Capture(Viewport);
            e.Handled = true;
        }
    }

    private void OnInputPinPressed(object? sender, PointerPressedEventArgs e)
        => e.Handled = true; // don't start a pan/drag when grabbing the input pin

    private void UpdateTempWire(Point viewportPos)
    {
        if (_connectOwner is null || _connectIndex < 0) return;
        // The temp wire lives in the same biased canvas space as the cards/wires, so
        // both endpoints carry NodeLayout.CanvasBias (see NodeLayout.CanvasBias).
        var start = new Point(
            NodeLayout.OutputPinX(_connectOwner) + NodeLayout.CanvasBias,
            NodeLayout.OutputPinY(_connectOwner, _connectIndex) + NodeLayout.CanvasBias);
        var world = ToWorld(viewportPos);
        var end = new Point(world.X + NodeLayout.CanvasBias, world.Y + NodeLayout.CanvasBias);
        TempWire.Data = Geometry.Parse(ConnectionViewModel.BuildPath(start, end));
    }

    private static NodeViewModel? NodeAt(Point world, EditorViewModel vm)
    {
        foreach (var n in vm.Nodes)
        {
            var h = NodeLayout.NodeHeight(n);
            if (world.X >= n.X && world.X <= n.X + NodeLayout.Width &&
                world.Y >= n.Y && world.Y <= n.Y + h)
                return n;
        }
        return null;
    }

    // ---- zoom to fit --------------------------------------------------------

    private void FitToView()
    {
        if (Vm is not { } vm || vm.Nodes.Count == 0) return;
        var bounds = Viewport.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var n in vm.Nodes)
        {
            var h = NodeLayout.NodeHeight(n);
            minX = Math.Min(minX, n.X);
            minY = Math.Min(minY, n.Y);
            maxX = Math.Max(maxX, n.X + NodeLayout.Width);
            maxY = Math.Max(maxY, n.Y + h);
        }

        // Also include entities in the fit calculation
        foreach (var en in vm.Entities)
        {
            minX = Math.Min(minX, en.X);
            minY = Math.Min(minY, en.Y);
            maxX = Math.Max(maxX, en.X + NodeLayout.EntityWidth);
            maxY = Math.Max(maxY, en.Y + NodeLayout.EntityHeight);
        }

        const double margin = 60;
        var w = Math.Max(1, maxX - minX);
        var h2 = Math.Max(1, maxY - minY);
        vm.SetZoom(Math.Min((bounds.Width - 2 * margin) / w, (bounds.Height - 2 * margin) / h2));

        var cx = (minX + maxX) / 2;
        var cy = (minY + maxY) / 2;
        vm.OffsetX = bounds.Width / 2 - cx * vm.Zoom;
        vm.OffsetY = bounds.Height / 2 - cy * vm.Zoom;
    }
}
