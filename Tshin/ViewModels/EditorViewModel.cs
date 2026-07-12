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
/// Right-pane Blueprints editor: owns the node graph, entities/components, the wires
/// between choices and nodes, and the canvas viewport (pan/zoom). Builds from a
/// <see cref="StorySnapshot"/> and writes a fresh snapshot back on save.
/// </summary>
public partial class EditorViewModel : ViewModelBase, IEditorContext
{
    private readonly IProjectService _projectService;
    private readonly string _projectId;
    private int _newNodeCounter;
    private int _newEntityCounter;

    [ObservableProperty]
    private string _projectName;

    public ObservableCollection<NodeViewModel> Nodes { get; } = new();
    public ObservableCollection<ConnectionViewModel> Connections { get; } = new();
    public ObservableCollection<EntityViewModel> Entities { get; } = new();

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private NodeViewModel? _selectedNode;

    [ObservableProperty]
    private ChoiceViewModel? _selectedChoice;

    [ObservableProperty]
    private EntityViewModel? _selectedEntity;

    [ObservableProperty]
    private ComponentViewModel? _selectedComponent;

    /// <summary>
    /// The single object the right-hand inspector should edit. A selected component wins
    /// (with the entity kept as context for the breadcrumb), then choice, entity, node.
    /// Drives one inspector host instead of four overlapping panels.
    /// </summary>
    public object? SelectedInspectorTarget
        => (object?)SelectedComponent ?? SelectedChoice ?? (object?)SelectedEntity ?? SelectedNode;

    partial void OnProjectNameChanged(string value) => NoteContinuousChange(this, "projectname");
    partial void OnSelectedNodeChanged(NodeViewModel? value) => OnPropertyChanged(nameof(SelectedInspectorTarget));
    partial void OnSelectedChoiceChanged(ChoiceViewModel? value) => OnPropertyChanged(nameof(SelectedInspectorTarget));
    partial void OnSelectedEntityChanged(EntityViewModel? value) => OnPropertyChanged(nameof(SelectedInspectorTarget));
    partial void OnSelectedComponentChanged(ComponentViewModel? value) => OnPropertyChanged(nameof(SelectedInspectorTarget));

    /// <summary>Breadcrumb from the component inspector back up to its owning entity.</summary>
    [RelayCommand]
    public void BackToEntity() => SelectedComponent = null;

    [ObservableProperty]
    private bool _snapToGrid;

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

    // The canvas content is laid out with a fixed CanvasBias offset (see
    // NodeLayout.CanvasBias) so cards at any world coordinate stay inside the canvas's
    // bounds and aren't culled. The render translate cancels that bias, so on screen
    // world origin still lands at (OffsetX, OffsetY): screen = world*Zoom + Offset.
    public double RenderOffsetX => OffsetX - NodeLayout.CanvasBias * Zoom;
    public double RenderOffsetY => OffsetY - NodeLayout.CanvasBias * Zoom;

    partial void OnZoomChanged(double value)
    {
        OnPropertyChanged(nameof(RenderOffsetX));
        OnPropertyChanged(nameof(RenderOffsetY));
    }

    partial void OnOffsetXChanged(double value) => OnPropertyChanged(nameof(RenderOffsetX));
    partial void OnOffsetYChanged(double value) => OnPropertyChanged(nameof(RenderOffsetY));

    public EditorViewModel(StorySnapshot snapshot, string projectName, IProjectService projectService)
    {
        _projectService = projectService;
        _projectId = snapshot.ProjectId;
        _projectName = projectName;
        Nodes.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNodes));
        BuildFrom(snapshot);

        // When entities change, refresh AvailableEntities on all choices
        Entities.CollectionChanged += (_, _) => RefreshAvailableEntitiesOnChoices();

        InitHistory();
    }

    /// <summary>Snaps a world coordinate to the grid when snapping is enabled.</summary>
    public double Snap(double value) => SnapToGrid ? Math.Round(value / GridSize) * GridSize : value;

    // MarkDirty and the undo/redo history live in EditorViewModel.History.cs.

    // ---- graph construction -------------------------------------------------

    private void BuildFrom(StorySnapshot snapshot)
    {
        // --- Build entities ---
        foreach (var es in snapshot.Entities)
        {
            var evm = new EntityViewModel(es.Id, es.Name, es.X, es.Y, this) { Visible = es.Visible };
            foreach (var cs in es.Components)
            {
                var cvm = ComponentFromSnapshot(cs, this);
                if (cvm is not null)
                    evm.Components.Add(cvm);
            }
            Entities.Add(evm);
        }

        // --- Build nodes ---
        var byId = new Dictionary<string, NodeViewModel>();
        foreach (var n in snapshot.Nodes)
        {
            var vm = new NodeViewModel(n.Id, n.DisplayText, n.X, n.Y, this);
            Nodes.Add(vm);
            byId[n.Id] = vm;
        }

        foreach (var n in snapshot.Nodes)
        {
            var owner = byId[n.Id];
            foreach (var c in n.Choices)
            {
                NodeViewModel? target = c.TargetNodeId is not null && byId.TryGetValue(c.TargetNodeId, out var t) ? t : null;
                var choiceVm = new ChoiceViewModel(c.DisplayText, target, this);
                choiceVm.AvailableEntities = Entities;
                // Restore the condition tree (AvailableEntities is set, so pickers resolve).
                if (c.Condition is not null)
                    choiceVm.Condition = c.Condition;
                choiceVm.ConditionFalseBehavior = c.ConditionFalseBehavior;
                // Build commands from snapshot
                foreach (var cmd in c.Commands)
                {
                    var cmdVm = CommandFromSnapshot(cmd, Entities, this);
                    if (cmdVm is not null)
                    {
                        // Restore the command's condition tree (AvailableEntities is set, so pickers resolve).
                        if (cmd.Condition is not null)
                            cmdVm.Condition = cmd.Condition;
                        choiceVm.Commands.Add(cmdVm);
                    }
                }
                owner.Choices.Add(choiceVm);
            }
        }

        RebuildConnections();
        IsDirty = false;
    }

    private static ComponentViewModel? ComponentFromSnapshot(ComponentSnapshot cs, IEditorContext context)
    {
        ComponentViewModel? cvm = cs switch
        {
            NumberComponentSnapshot n => new NumberComponentViewModel(n.Name, n.Value, n.MinValue, n.MaxValue, context),
            TextComponentSnapshot t => new TextComponentViewModel(t.Name, t.Value, context),
            ConditionComponentSnapshot c => new ConditionComponentViewModel(c.Name, c.Value, context),
            _ => null
        };
        if (cvm is not null) cvm.Visible = cs.Visible;
        return cvm;
    }

    private static CommandViewModel? CommandFromSnapshot(CommandSnapshot cs, ObservableCollection<EntityViewModel> entities, IEditorContext context)
    {
        var targetEntity = entities.FirstOrDefault(e => e.Id == cs.TargetEntityId);
        return cs switch
        {
            ModifyNumberCommandSnapshot n => new CommandViewModel(
                targetEntity, n.TargetComponentName, n.Field, n.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                n.Value, false, entities, context),
            ModifyTextCommandSnapshot t => new CommandViewModel(
                targetEntity, t.TargetComponentName, "Set", t.Value,
                0, false, entities, context),
            ModifyBooleanCommandSnapshot b => new CommandViewModel(
                targetEntity, b.TargetComponentName, "Set", b.Value.ToString(),
                0, b.Value, entities, context),
            _ => null
        };
    }

    // ---- entity editing API -------------------------------------------------

    public EntityViewModel CreateEntityAt(double worldX, double worldY)
    {
        var id = Guid.NewGuid().ToString("D");
        var name = $"entity_{++_newEntityCounter}";
        var evm = new EntityViewModel(id, name, worldX, worldY, this);
        Entities.Add(evm);
        RefreshAvailableEntitiesOnChoices();
        MarkDirty();
        return evm;
    }

    public void AddComponentToEntity(EntityViewModel entity, string componentType)
    {
        ComponentViewModel comp = componentType switch
        {
            "number" => new NumberComponentViewModel("New Number", 0, 0, double.MaxValue, this),
            "text" => new TextComponentViewModel("New Text", "", this),
            "condition" => new ConditionComponentViewModel("New Condition", false, this),
            _ => throw new ArgumentException($"Unknown component type: {componentType}")
        };
        entity.Components.Add(comp);
        MarkDirty();
    }

    public void RemoveComponentFromEntity(EntityViewModel entity, ComponentViewModel component)
    {
        entity.Components.Remove(component);
        if (SelectedComponent == component) SelectedComponent = null;
        MarkDirty();
    }

    private void RefreshAvailableEntitiesOnChoices()
    {
        foreach (var node in Nodes)
        {
            foreach (var choice in node.Choices)
            {
                choice.AvailableEntities = Entities;
            }
        }
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
    private void AddEntity()
    {
        var worldX = (-OffsetX + 300) / Zoom;
        var worldY = (-OffsetY + 260) / Zoom;
        CreateEntityAt(worldX, worldY);
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

    /// <summary>Raised when there is no project file yet and we need the file-picker.</summary>
    public event Action? RequestExport;

    [RelayCommand]
    private void Run()
    {
        var startNode = Nodes.FirstOrDefault();
        if (startNode is null) return;
        var player = new PlayerViewModel(startNode, Entities);
        RequestPlay?.Invoke(player);
    }

    /// <summary>Raised when play mode is requested; the view opens a PlayerWindow.</summary>
    public event Action<PlayerViewModel>? RequestPlay;

    // ---- persistence --------------------------------------------------------

    [RelayCommand]
    private async Task Save()
    {
        var path = await _projectService.GetProjectFilePathAsync(_projectId);
        if (string.IsNullOrEmpty(path))
        {
            RequestExport?.Invoke();
            return;
        }

        await _projectService.SaveProjectAsync(BuildSnapshot());
        IsDirty = false;
    }

    [RelayCommand]
    private async Task Export(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        await _projectService.ExportProjectAsync(BuildSnapshot(), filePath);
    }

    private StorySnapshot BuildSnapshot()
    {
        var snapshot = new StorySnapshot { ProjectId = _projectId };

        // Entities
        foreach (var evm in Entities)
        {
            var es = new EntitySnapshot
            {
                Id = evm.Id,
                Name = evm.Name,
                X = evm.X,
                Y = evm.Y,
                Visible = evm.Visible,
            };
            foreach (var cvm in evm.Components)
            {
                ComponentSnapshot? cs = cvm switch
                {
                    NumberComponentViewModel n => new NumberComponentSnapshot(n.Name, n.Value, n.MinValue, n.MaxValue),
                    TextComponentViewModel t => new TextComponentSnapshot(t.Name, t.Value),
                    ConditionComponentViewModel c => new ConditionComponentSnapshot(c.Name, c.Value),
                    _ => null
                };
                if (cs is not null)
                {
                    cs.Visible = cvm.Visible;
                    es.Components.Add(cs);
                }
            }
            snapshot.Entities.Add(es);
        }

        // Nodes
        foreach (var node in Nodes)
        {
            var ns = new NodeSnapshot
            {
                Id = node.Id,
                DisplayText = node.DisplayText,
                X = node.X,
                Y = node.Y,
            };
            foreach (var choice in node.Choices)
            {
                var cs = new ChoiceSnapshot
                {
                    DisplayText = choice.DisplayText,
                    TargetNodeId = choice.Target?.Id,
                    Condition = choice.Condition,
                    ConditionFalseBehavior = choice.ConditionFalseBehavior,
                };
                foreach (var cmdVm in choice.Commands)
                {
                    var cmd = BuildCommandSnapshot(cmdVm);
                    if (cmd is not null)
                        cs.Commands.Add(cmd);
                }
                ns.Choices.Add(cs);
            }
            snapshot.Nodes.Add(ns);
        }

        return snapshot;
    }

    private static CommandSnapshot? BuildCommandSnapshot(CommandViewModel cmdVm)
    {
        if (cmdVm.TargetEntity is null) return null;

        var field = cmdVm.DisplayFieldValue;
        var entityId = cmdVm.TargetEntity.Id;
        var componentName = cmdVm.TargetComponentName;
        if (string.IsNullOrEmpty(componentName)) return null;

        CommandSnapshot? snapshot = cmdVm.TargetComponentType switch
        {
            "number" => new ModifyNumberCommandSnapshot(entityId, componentName, field, cmdVm.NumberValue),
            "text" => new ModifyTextCommandSnapshot(entityId, componentName, cmdVm.TextValue),
            "condition" => new ModifyBooleanCommandSnapshot(entityId, componentName, cmdVm.BoolValue),
            _ => null
        };
        if (snapshot is not null)
            snapshot.Condition = cmdVm.Condition;
        return snapshot;
    }
}