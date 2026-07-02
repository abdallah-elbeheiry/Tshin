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
public partial class EditorViewModel : ViewModelBase
{
    private readonly IProjectService _projectService;
    private readonly string _projectId;
    private int _newNodeCounter;
    private int _newEntityCounter;

    public string ProjectName { get; }

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

        // When entities change, refresh AvailableEntities on all choices
        Entities.CollectionChanged += (_, _) => RefreshAvailableEntitiesOnChoices();
    }

    /// <summary>Snaps a world coordinate to the grid when snapping is enabled.</summary>
    public double Snap(double value) => SnapToGrid ? Math.Round(value / GridSize) * GridSize : value;

    internal void MarkDirty() => IsDirty = true;

    // ---- graph construction -------------------------------------------------

    private void BuildFrom(StorySnapshot snapshot)
    {
        // --- Build entities ---
        foreach (var es in snapshot.Entities)
        {
            var evm = new EntityViewModel(es.Id, es.Name, es.X, es.Y, MarkDirty);
            foreach (var cs in es.Components)
            {
                var cvm = ComponentFromSnapshot(cs, MarkDirty);
                if (cvm is not null)
                    evm.Components.Add(cvm);
            }
            Entities.Add(evm);
        }

        // --- Build nodes ---
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
                var choiceVm = new ChoiceViewModel(c.DisplayText, target, MarkDirty);
                choiceVm.AvailableEntities = Entities;
                // Build commands from snapshot
                foreach (var cmd in c.Commands)
                {
                    var cmdVm = CommandFromSnapshot(cmd, Entities, MarkDirty);
                    if (cmdVm is not null)
                        choiceVm.Commands.Add(cmdVm);
                }
                owner.Choices.Add(choiceVm);
            }
        }

        RebuildConnections();
        IsDirty = false;
    }

    private static ComponentViewModel? ComponentFromSnapshot(ComponentSnapshot cs, Action onChanged)
    {
        return cs switch
        {
            NumberComponentSnapshot n => new NumberComponentViewModel(n.Name, n.Value, n.MinValue, n.MaxValue, onChanged),
            TextComponentSnapshot t => new TextComponentViewModel(t.Name, t.Value, onChanged),
            ConditionComponentSnapshot c => new ConditionComponentViewModel(c.Name, c.Value, onChanged),
            _ => null
        };
    }

    private static CommandViewModel? CommandFromSnapshot(CommandSnapshot cs, ObservableCollection<EntityViewModel> entities, Action onChanged)
    {
        var targetEntity = entities.FirstOrDefault(e => e.Id == cs.TargetEntityId);
        return cs switch
        {
            ModifyNumberCommandSnapshot n => new CommandViewModel(
                targetEntity, n.TargetComponentName, n.Field, n.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                n.Value, false, entities, onChanged),
            ModifyTextCommandSnapshot t => new CommandViewModel(
                targetEntity, t.TargetComponentName, "Set", t.Value,
                0, false, entities, onChanged),
            ModifyBooleanCommandSnapshot b => new CommandViewModel(
                targetEntity, b.TargetComponentName, "Set", b.Value.ToString(),
                0, b.Value, entities, onChanged),
            _ => null
        };
    }

    // ---- entity editing API -------------------------------------------------

    public EntityViewModel CreateEntityAt(double worldX, double worldY)
    {
        var id = Guid.NewGuid().ToString("D");
        var name = $"entity_{++_newEntityCounter}";
        var evm = new EntityViewModel(id, name, worldX, worldY, MarkDirty);
        Entities.Add(evm);
        RefreshAvailableEntitiesOnChoices();
        MarkDirty();
        return evm;
    }

    public void AddComponentToEntity(EntityViewModel entity, string componentType)
    {
        ComponentViewModel comp = componentType switch
        {
            "number" => new NumberComponentViewModel("New Number", 0, 0, double.MaxValue, MarkDirty),
            "text" => new TextComponentViewModel("New Text", "", MarkDirty),
            "condition" => new ConditionComponentViewModel("New Condition", false, MarkDirty),
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
        Player = new PlayerViewModel(startNode, Entities, MarkDirty);
    }

    [RelayCommand]
    private void CloseRun() => Player = null;

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
                    es.Components.Add(cs);
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

        return cmdVm.TargetComponentType switch
        {
            "number" => new ModifyNumberCommandSnapshot(entityId, componentName, field, cmdVm.NumberValue),
            "text" => new ModifyTextCommandSnapshot(entityId, componentName, cmdVm.TextValue),
            "condition" => new ModifyBooleanCommandSnapshot(entityId, componentName, cmdVm.BoolValue),
            _ => null
        };
    }
}