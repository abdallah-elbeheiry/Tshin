using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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
                var clone = new EntityViewModel(e.Id, e.Name, e.X, e.Y, onChanged);
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
            NumberComponentViewModel n => new NumberComponentViewModel(n.Name, n.Value, n.MinValue, n.MaxValue, onChanged),
            TextComponentViewModel t => new TextComponentViewModel(t.Name, t.Value, onChanged),
            ConditionComponentViewModel cnd => new ConditionComponentViewModel(cnd.Name, cnd.Value, onChanged),
            _ => null
        };
    }

    public bool IsEnd => CurrentNode is null || CurrentNode.Choices.Count == 0;

    partial void OnCurrentNodeChanged(NodeViewModel? value) => OnPropertyChanged(nameof(IsEnd));

    [RelayCommand]
    private void Choose(ChoiceViewModel? choice)
    {
        if (choice?.Target is { } target)
        {
            // Execute commands on play entities before transitioning
            foreach (var cmd in choice.Commands)
            {
                ExecuteCommand(cmd);
            }
            CurrentNode = target;
        }
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