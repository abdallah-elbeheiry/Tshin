using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Tshin.ViewModels;

namespace Tshin.Views;

public partial class EditorView : UserControl
{
    private enum Mode { None, Pan, Node, Connect }

    private Mode _mode;
    private Point _last;
    private NodeViewModel? _dragNode;
    private ChoiceViewModel? _connectChoice;
    private NodeViewModel? _connectOwner;
    private int _connectIndex = -1;

    private EditorViewModel? _vm;

    public EditorView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        World.Background = CreateGridBrush();
    }

    private static VisualBrush CreateGridBrush()
    {
        const double cell = 26;
        var dot = new Ellipse
        {
            Width = 2,
            Height = 2,
            Fill = new SolidColorBrush(Color.Parse("#2E2E33")),
        };
        Canvas.SetLeft(dot, cell / 2);
        Canvas.SetTop(dot, cell / 2);

        var tile = new Canvas { Width = cell, Height = cell, Children = { dot } };

        return new VisualBrush
        {
            Visual = tile,
            TileMode = TileMode.Tile,
            Stretch = Stretch.None,
            SourceRect = new RelativeRect(0, 0, cell, cell, RelativeUnit.Absolute),
            DestinationRect = new RelativeRect(0, 0, cell, cell, RelativeUnit.Absolute),
        };
    }

    private EditorViewModel? Vm => DataContext as EditorViewModel;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
        {
            _vm.RequestFit -= FitToView;
            _vm.RequestExport -= OnRequestExport;
        }
        _vm = Vm;
        if (_vm is not null)
        {
            _vm.RequestFit += FitToView;
            _vm.RequestExport += OnRequestExport;
        }
    }

    private void OnRequestExport() => OnExportClick(null, new RoutedEventArgs());

    private Point ToWorld(Point p)
        => Vm is { } vm ? new Point((p.X - vm.OffsetX) / vm.Zoom, (p.Y - vm.OffsetY) / vm.Zoom) : p;

    // ---- viewport (pan / background / zoom) --------------------------------

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

    private Point _lastRightClickPosition;

    private void OnCreateNodeClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm) return;
        var world = ToWorld(_lastRightClickPosition);
        vm.CreateNodeAt(world.X - NodeLayout.Width / 2, world.Y);
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Story",
            FileTypeChoices = new List<FilePickerFileType>
            {
                new("Tshin Story Files") { Patterns = new[] { "*.tshin" } }
            },
            DefaultExtension = "tshin",
            SuggestedFileName = $"{vm.ProjectName}.tshin"
        });

        if (file != null)
        {
            await vm.ExportCommand.ExecuteAsync(file.Path.LocalPath);
        }
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm) return;
        await vm.SaveCommand.ExecuteAsync(null);
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
        }
        else if (Vm is { } v && _mode == Mode.Node && _dragNode is not null)
        {
            _dragNode.X = v.Snap(_dragNode.X);
            _dragNode.Y = v.Snap(_dragNode.Y);
        }

        TempWire.IsVisible = false;
        _mode = Mode.None;
        _dragNode = null;
        _connectChoice = null;
        _connectOwner = null;
        _connectIndex = -1;
        e.Pointer.Capture(null);
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
        var start = new Point(
            NodeLayout.OutputPinX(_connectOwner),
            NodeLayout.OutputPinY(_connectOwner, _connectIndex));
        var end = ToWorld(viewportPos);
        TempWire.Data = Geometry.Parse(ConnectionViewModel.BuildPath(start, end));
    }

    private static NodeViewModel? NodeAt(Point world, EditorViewModel vm)
    {
        foreach (var n in vm.Nodes)
        {
            var h = NodeLayout.ChoicesTop + n.Choices.Count * NodeLayout.ChoiceRowHeight + 44;
            if (world.X >= n.X && world.X <= n.X + NodeLayout.Width &&
                world.Y >= n.Y && world.Y <= n.Y + h)
                return n;
        }
        return null;
    }

    // ---- keyboard -----------------------------------------------------------

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is not (Key.Delete or Key.Back)) return;
        if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox) return;
        if (Vm is { SelectedNode: not null } vm && vm.RemoveNodeCommand.CanExecute(null))
        {
            vm.RemoveNodeCommand.Execute(null);
            e.Handled = true;
        }
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
            var h = NodeLayout.ChoicesTop + n.Choices.Count * NodeLayout.ChoiceRowHeight + 44;
            minX = Math.Min(minX, n.X);
            minY = Math.Min(minY, n.Y);
            maxX = Math.Max(maxX, n.X + NodeLayout.Width);
            maxY = Math.Max(maxY, n.Y + h);
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
