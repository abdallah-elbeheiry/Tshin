using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tshin.Core.Models;
using Tshin.Core.Utils.Managers;
using Tshin.Utilities;

namespace Tshin.ViewModels;

/// <summary>
/// A play-through of the current graph (Run mode). Walks the live
/// <see cref="NodeViewModel"/> graph, so unsaved edits are playable immediately.
/// Operates on a cloned snapshot of entity state so author data is not mutated.
/// </summary>
public partial class PlayerViewModel : ViewModelBase
{
    private readonly NodeViewModel? _start;
    private readonly ObservableCollection<NodeViewModel> _allNodes;

    /// <summary>
    /// Cloned entity view models used during play. Mutations (commands) affect these,
    /// not the live editor entities.
    /// </summary>
    public ObservableCollection<EntityViewModel> PlayEntities { get; } = new();

    [ObservableProperty]
    private NodeViewModel? _currentNode;

    /// <summary>The raw display text of the current node, before interpolation.</summary>
    public string CurrentNodeDisplayTextRaw => CurrentNode?.DisplayText ?? string.Empty;

    /// <summary>
    /// The current node's display text with <c>{UUID.Component}</c> tokens resolved
    /// against the live play-state entity manager.
    /// </summary>
    public string CurrentNodeDisplayTextResolved
    {
        get
        {
            var em = BuildEntityManagerFromPlayEntities();
            return VariableInterpolator.Interpolate(CurrentNode?.DisplayText, em);
        }
    }

    /// <summary>
    /// The choices offered on the current node, projected for play: each carries whether
    /// it is openable (its condition holds), and Hidden choices are omitted entirely.
    /// Rebuilt whenever the node changes or a choice's commands mutate game state.
    /// </summary>
    public ObservableCollection<PlayChoiceViewModel> CurrentChoices { get; } = new();

    public PlayerViewModel(NodeViewModel? start,
                           ObservableCollection<EntityViewModel>? liveEntities,
                           Action onChanged)
    {
        _start = start;
        _currentNode = start;
        _allNodes = new ObservableCollection<NodeViewModel>();

        // Clone entities so play mode mutations don't affect the editor state
        if (liveEntities is not null)
        {
            foreach (var e in liveEntities)
            {
                var clone = new EntityViewModel(e.Id, e.Name, e.X, e.Y, onChanged) { Visible = e.Visible };
                foreach (var c in e.Components)
                {
                    var compClone = CloneComponent(c, onChanged);
                    if (compClone is not null)
                        clone.Components.Add(compClone);
                }
                PlayEntities.Add(clone);
            }
        }

        // Build a flat list for navigation
        _allNodes.Clear();
        // Walk the graph from start to find all reachable nodes (simple approach)
        if (start is not null)
        {
            CollectNodes(start, _allNodes);
        }

        RefreshChoices();
    }

    /// <summary>
    /// Rebuilds <see cref="CurrentChoices"/> for the current node: evaluates each choice's
    /// condition against live play state, marks it (Close) openable/blocked, and skips
    /// choices whose false-behavior is Hide.
    /// </summary>
    private void RefreshChoices()
    {
        CurrentChoices.Clear();
        if (CurrentNode is null) return;

        var em = BuildEntityManagerFromPlayEntities();
        foreach (var choice in CurrentNode.Choices)
        {
            var condition = choice.Condition;
            var openable = condition is null || condition.Evaluate(em);
            if (!openable && choice.ConditionFalseBehavior == ConditionFalseBehavior.Hide)
                continue;
            var resolved = VariableInterpolator.Interpolate(choice.DisplayText, em);
            CurrentChoices.Add(new PlayChoiceViewModel(choice, openable, resolved));
        }
    }

    private static void CollectNodes(NodeViewModel? node, ObservableCollection<NodeViewModel> nodes)
    {
        if (node is null || nodes.Contains(node)) return;
        nodes.Add(node);
        foreach (var choice in node.Choices)
        {
            if (choice.Target is not null)
                CollectNodes(choice.Target, nodes);
        }
    }

    private static ComponentViewModel? CloneComponent(ComponentViewModel c, Action onChanged)
    {
        return c switch
        {
            NumberComponentViewModel n => new NumberComponentViewModel(n.Name, n.Value, n.MinValue, n.MaxValue, onChanged) { Visible = n.Visible },
            TextComponentViewModel t => new TextComponentViewModel(t.Name, t.Value, onChanged) { Visible = t.Visible },
            ConditionComponentViewModel cnd => new ConditionComponentViewModel(cnd.Name, cnd.Value, onChanged) { Visible = cnd.Visible },
            _ => null
        };
    }

    public bool IsEnd => CurrentNode is null || CurrentNode.Choices.Count == 0;

    // ── Entities panel ────────────────────────────────────────────────────

    /// <summary>Entities flagged Visible, shown in the player's collapsible panel.</summary>
    public IEnumerable<EntityViewModel> VisiblePlayEntities => PlayEntities.Where(e => e.Visible);

    /// <summary>Whether there is anything to show in the entities panel at all.</summary>
    public bool HasVisibleEntities => PlayEntities.Any(e => e.Visible);

    [ObservableProperty]
    private bool _isEntitiesPanelExpanded = true;

    public string EntitiesToggleGlyph => IsEntitiesPanelExpanded ? "▾" : "▸";

    partial void OnIsEntitiesPanelExpandedChanged(bool value)
        => OnPropertyChanged(nameof(EntitiesToggleGlyph));

    [RelayCommand]
    private void ToggleEntitiesPanel() => IsEntitiesPanelExpanded = !IsEntitiesPanelExpanded;

    partial void OnCurrentNodeChanged(NodeViewModel? value)
    {
        OnPropertyChanged(nameof(IsEnd));
        OnPropertyChanged(nameof(CurrentNodeDisplayTextRaw));
        OnPropertyChanged(nameof(CurrentNodeDisplayTextResolved));
        RefreshChoices();
    }

    [RelayCommand]
    private void Choose(ChoiceViewModel? choice)
    {
        if (choice is null) return;

        // Evaluate the condition if one exists; the choice is blocked if it fails.
        if (choice.Condition is not null)
        {
            var em = BuildEntityManagerFromPlayEntities();
            if (!choice.Condition.Evaluate(em))
                return;
        }

        // Execute all mutation commands for this choice.
        foreach (var cmd in choice.Commands)
            ExecuteCommand(cmd);

        // Navigate only when the choice is linked to a node; a targetless choice
        // stays on the current node (its commands have already run).
        if (choice.Target is { } target)
            CurrentNode = target;
        else
        {
            // Same-node re-evaluation: commands may have changed values.
            OnPropertyChanged(nameof(CurrentNodeDisplayTextResolved));
            RefreshChoices();
        }
    }

    /// <summary>
    /// Builds a temporary <see cref="EntityManager"/> from the play-mode entity clones
    /// so that condition tree nodes can evaluate against live game state.
    /// </summary>
    private EntityManager BuildEntityManagerFromPlayEntities()
    {
        var em = new EntityManager();
        foreach (var evm in PlayEntities)
        {
            var entity = em.CreateEntity(Guid.Parse(evm.Id));
            entity.Name = evm.Name;
            entity.X = evm.X;
            entity.Y = evm.Y;
            entity.Visible = evm.Visible;

            foreach (var cvm in evm.Components)
            {
                switch (cvm)
                {
                    case NumberComponentViewModel n:
                        em.SetComponent(entity, new NumberComponent
                        {
                            Name = n.Name, Value = n.Value, MinValue = n.MinValue, MaxValue = n.MaxValue, Visible = n.Visible
                        });
                        break;
                    case TextComponentViewModel t:
                        em.SetComponent(entity, new TextComponent
                        {
                            Name = t.Name, Value = t.Value, Visible = t.Visible
                        });
                        break;
                    case ConditionComponentViewModel c:
                        em.SetComponent(entity, new ConditionComponent
                        {
                            Name = c.Name, Value = c.Value, Visible = c.Visible
                        });
                        break;
                }
            }
        }
        return em;
    }

    private void ExecuteCommand(CommandViewModel cmd)
    {
        if (cmd.TargetEntity is null) return;

        // Find the play-mode clone of the target entity
        var playEntity = PlayEntities.FirstOrDefault(e => e.Id == cmd.TargetEntity.Id);
        if (playEntity is null) return;

        // Apply the mutation based on the command type
        var component = playEntity.Components
            .FirstOrDefault(c => c.Name.Equals(cmd.TargetComponentName, StringComparison.OrdinalIgnoreCase));

        if (component is null) return;

        switch (component)
        {
        case NumberComponentViewModel num when double.TryParse(cmd.NumberValue.ToString(), 
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var parsedNum):
                var field = cmd.DisplayFieldValue;
                num.Value = field switch
                {
                    "Increase" => Math.Clamp(num.Value + parsedNum, num.MinValue, num.MaxValue),
                    "Reduce" => Math.Clamp(num.Value - parsedNum, num.MinValue, num.MaxValue),
                    _ => Math.Clamp(parsedNum, num.MinValue, num.MaxValue)
                };
                break;

            case TextComponentViewModel text:
                text.Value = cmd.TextValue;
                break;

            case ConditionComponentViewModel condition:
                condition.Value = cmd.BoolValue;
                break;
        }
    }

    [RelayCommand]
    private void Restart() => CurrentNode = _start;
}

/// <summary>
/// A choice as presented in the player: the underlying <see cref="ChoiceViewModel"/> plus
/// whether it is currently openable. Close-behavior choices appear disabled when not
/// openable; Hide-behavior choices are absent from <see cref="PlayerViewModel.CurrentChoices"/>.
/// </summary>
public partial class PlayChoiceViewModel : ViewModelBase
{
    public ChoiceViewModel Choice { get; }

    public string DisplayText => Choice.DisplayText;

    /// <summary>
    /// The <c>{UUID.Component}</c>-resolved text for this choice. Updated on each
    /// choice rebuild so the UI shows live values after command mutations.
    /// </summary>
    [ObservableProperty]
    private string _resolvedDisplayText;

    [ObservableProperty]
    private bool _isOpenable;

    public PlayChoiceViewModel(ChoiceViewModel choice, bool isOpenable, string resolvedDisplayText)
    {
        Choice = choice;
        _isOpenable = isOpenable;
        _resolvedDisplayText = resolvedDisplayText;
    }
}
