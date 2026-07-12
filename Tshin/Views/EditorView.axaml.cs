using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Tshin.ViewModels;

namespace Tshin.Views;

/// <summary>
/// The editor shell: toolbar, the <see cref="CanvasView"/>, and the inspector host. The
/// canvas interactions (pan/zoom/drag/connect) live in <see cref="CanvasView"/>; this view
/// owns the toolbar commands, save/export/play plumbing, keyboard delete, and inspector resize.
/// </summary>
public partial class EditorView : UserControl
{
    private EditorViewModel? _vm;

    public EditorView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private EditorViewModel? Vm => DataContext as EditorViewModel;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
        {
            _vm.RequestExport -= OnRequestExport;
            _vm.RequestPlay -= OnRequestPlay;
        }
        _vm = Vm;
        if (_vm is not null)
        {
            _vm.RequestExport += OnRequestExport;
            _vm.RequestPlay += OnRequestPlay;
        }
    }

    private void OnRequestExport() => OnExportClick(null, new RoutedEventArgs());

    private void OnRequestPlay(PlayerViewModel player)
    {
        var window = new PlayerWindow { DataContext = player };
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is not null)
            window.Show(owner);
        else
            window.Show();
    }

    // ---- save / export ------------------------------------------------------

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var suggestedName = System.IO.Path.GetFileNameWithoutExtension(vm.ProjectName);
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Epic",
            FileTypeChoices = new List<FilePickerFileType>
            {
                new("Tshin Project") { Patterns = ["*.tshin"] }
            },
            DefaultExtension = "tshin",
            SuggestedFileName = suggestedName
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

    // ---- keyboard -----------------------------------------------------------

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is not (Key.Delete or Key.Back)) return;
        if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox) return;
        if (Vm is not { } vm) return;
        if (vm.SelectedComponent is not null)
        {
            vm.RemoveComponentFromEntityCommand.Execute(vm.SelectedComponent);
            e.Handled = true;
        }
        else if (vm.SelectedEntity is not null)
        {
            vm.RemoveEntityCommand.Execute(vm.SelectedEntity);
            e.Handled = true;
        }
        else if (vm.SelectedNode is not null && vm.RemoveNodeCommand.CanExecute(null))
        {
            vm.RemoveNodeCommand.Execute(null);
            e.Handled = true;
        }
    }

    // ---- inspector resize ---------------------------------------------------

    private void OnInspectorResize(object? sender, VectorEventArgs e)
    {
        // The grip sits on the LEFT edge of a right-anchored panel, so dragging left
        // (negative X) widens it. Clamp to a sensible range.
        var current = double.IsNaN(InspectorPanel.Width) ? InspectorPanel.Bounds.Width : InspectorPanel.Width;
        InspectorPanel.Width = Math.Clamp(current - e.Vector.X, 240, 640);
    }
}
