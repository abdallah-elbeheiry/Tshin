using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tshin.Models;
using Tshin.Services;

namespace Tshin.ViewModels;

/// <summary>
/// Right-pane Blueprints editor: owns the node graph, the wires between choices and
/// nodes, and the canvas viewport (pan/zoom). Builds from a <see cref="StorySnapshot"/>
/// and writes a fresh snapshot back on save.
/// </summary>
public partial class EditorViewModel : ViewModelBase
{
    private readonly IProjectService _projectService;
    private readonly string _projectId;
    private int _newNodeCounter;

    public string ProjectName { get; }

    public ObservableCollection<NodeViewModel> Nodes { get; } = new();
    public ObservableCollection<ConnectionViewModel> Connections { get; } = new();

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private NodeViewModel? _selectedNode;

    [ObservableProperty]
    private bool _snapToGrid;

    [ObservableProperty]
    private PlayerViewModel? _player;

    /// <summary>Grid cell used for snap-to-grid; matches the canvas dot spacing.</summary>
    public const double GridSize = 26;

    public bool HasNodes => Nodes.Count > 0;

    // Viewport transform (world -> screen): screen = world * Zoom + Offset.
    [ObservableProperty]
    private double _zoom = 1.0;

    [ObservableProperty]
    private double _offsetX;

    [ObservableProperty]
    private double _offsetY;

    public const double MinZoom = 0.25;
    public const double MaxZoom = 2.5;

    public EditorViewModel(StorySnapshot snapshot, string projectName, IProjectService projectService)
    {
        _projectService = projectService;
        _projectId = snapshot.ProjectId;
        ProjectName = projectName;
        Nodes.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNodes));
        BuildFrom(snapshot);
    }

    /// <summary>Snaps a world coordinate to the grid when snapping is enabled.</summary>
    public double Snap(double value) => SnapToGrid ? Math.Round(value / GridSize) * GridSize : value;

    // ---- graph construction -------------------------------------------------

    private void BuildFrom(StorySnapshot snapshot)
    {
        var byId = new Dictionary<string, NodeViewModel>();
        foreach (var n in snapshot.Nodes)
        {
            var vm = new NodeViewModel(n.Id, n.DisplayText, n.X, n.Y, MarkDirty);
            Nodes.Add(vm);
            byId[n.Id] = vm;
        }

        foreach (var n in snapshot.Nodes)
        {
            var owner = byId[n.Id];
            foreach (var c in n.Choices)
            {
                NodeViewModel? target = c.TargetNodeId is not null && byId.TryGetValue(c.TargetNodeId, out var t) ? t : null;
                owner.Choices.Add(new ChoiceViewModel(c.DisplayText, target, MarkDirty));
            }
        }

        RebuildConnections();
        IsDirty = false;
    }

    public void RebuildConnections()
    {
        foreach (var c in Connections) c.Dispose();
        Connections.Clear();

        foreach (var node in Nodes)
        {
            for (var i = 0; i < node.Choices.Count; i++)
            {
                var target = node.Choices[i].Target;
                if (target is not null)
                    Connections.Add(new ConnectionViewModel(node, target, i));
            }
        }
    }

    internal void MarkDirty() => IsDirty = true;

    // ---- editing API (called from view + commands) --------------------------

    public NodeViewModel CreateNodeAt(double worldX, double worldY)
    {
        var id = NextNodeId();
        var node = new NodeViewModel(id, "New node", worldX, worldY, MarkDirty);
        Nodes.Add(node);
        SelectNode(node);
        MarkDirty();
        return node;
    }

    private string NextNodeId()
    {
        string id;
        do { id = $"node_{++_newNodeCounter}"; }
        while (Nodes.Any(n => n.Id == id));
        return id;
    }

    public void SelectNode(NodeViewModel? node)
    {
        if (SelectedNode == node) return;
        if (SelectedNode is not null) SelectedNode.IsSelected = false;
        SelectedNode = node;
        if (node is not null) node.IsSelected = true;
    }

    [RelayCommand]
    private void RemoveNode(NodeViewModel? node)
    {
        node ??= SelectedNode;
        if (node is null) return;

        // Drop any choices pointing at the removed node.
        foreach (var other in Nodes)
            foreach (var choice in other.Choices.Where(c => c.Target == node).ToList())
                choice.Target = null;

        Nodes.Remove(node);
        if (SelectedNode == node) SelectedNode = null;
        RebuildConnections();
        MarkDirty();
    }

    [RelayCommand]
    private void AddChoice(NodeViewModel? node)
    {
        node ??= SelectedNode;
        node?.AddChoice();
        RebuildConnections();
    }

    [RelayCommand]
    private void RemoveChoice(ChoiceViewModel? choice)
    {
        if (choice is null) return;
        var owner = Nodes.FirstOrDefault(n => n.Choices.Contains(choice));
        if (owner is null) return;
        owner.Choices.Remove(choice);
        RebuildConnections();
        MarkDirty();
    }

    /// <summary>Links a choice to a target node (drag-to-connect or inspector).</summary>
    public void Connect(ChoiceViewModel choice, NodeViewModel target)
    {
        if (choice.Target == target) return;
        choice.Target = target;
        RebuildConnections();
        MarkDirty();
    }

    // ---- toolbar commands ---------------------------------------------------

    [RelayCommand]
    private void AddNode()
    {
        // Drop new nodes near the current viewport centre in world space.
        var worldX = (-OffsetX + 300) / Zoom;
        var worldY = (-OffsetY + 200) / Zoom;
        CreateNodeAt(worldX, worldY);
    }

    [RelayCommand]
    private void ZoomIn() => SetZoom(Zoom * 1.2);

    [RelayCommand]
    private void ZoomOut() => SetZoom(Zoom / 1.2);

    public void SetZoom(double zoom) => Zoom = Math.Clamp(zoom, MinZoom, MaxZoom);

    [RelayCommand]
    private void ZoomToFit() => RequestFit?.Invoke();

    /// <summary>Raised so the view (which knows the viewport size) can frame all nodes.</summary>
    public event Action? RequestFit;

    [RelayCommand]
    private void Run() => Player = new PlayerViewModel(Nodes.FirstOrDefault());

    [RelayCommand]
    private void CloseRun() => Player = null;

    // ---- persistence --------------------------------------------------------

    [RelayCommand]
    private async Task Save()
    {
        await _projectService.SaveProjectAsync(BuildSnapshot());
        IsDirty = false;
    }

    private StorySnapshot BuildSnapshot()
    {
        var snapshot = new StorySnapshot { ProjectId = _projectId };
        foreach (var node in Nodes)
        {
            snapshot.Nodes.Add(new NodeSnapshot
            {
                Id = node.Id,
                DisplayText = node.DisplayText,
                X = node.X,
                Y = node.Y,
                Choices = node.Choices.Select(c => new ChoiceSnapshot
                {
                    DisplayText = c.DisplayText,
                    TargetNodeId = c.Target?.Id,
                }).ToList(),
            });
        }
        return snapshot;
    }
}
