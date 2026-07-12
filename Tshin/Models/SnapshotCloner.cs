using System.Linq;
using Tshin.Core.Models;
using Tshin.Core.Utils.Systems;

namespace Tshin.Models;

/// <summary>
/// Deep-clones <see cref="StorySnapshot"/> trees. Used by the project services to hand
/// editors their own mutable copy, and by the editor's undo/redo history to capture
/// independent point-in-time snapshots.
/// </summary>
public static class SnapshotCloner
{
    public static StorySnapshot Clone(StorySnapshot source) => new()
    {
        ProjectId = source.ProjectId,
        Nodes = source.Nodes.Select(n => new NodeSnapshot
        {
            Id = n.Id,
            DisplayText = n.DisplayText,
            X = n.X,
            Y = n.Y,
            Choices = n.Choices.Select(c => new ChoiceSnapshot
            {
                DisplayText = c.DisplayText,
                TargetNodeId = c.TargetNodeId,
                Condition = CloneCondition(c.Condition),
                ConditionFalseBehavior = c.ConditionFalseBehavior,
                Commands = c.Commands.Select(CloneCommand).ToList(),
            }).ToList(),
        }).ToList(),
        Entities = source.Entities.Select(e => new EntitySnapshot
        {
            Id = e.Id,
            Name = e.Name,
            X = e.X,
            Y = e.Y,
            Visible = e.Visible,
            Components = e.Components.Select(CloneComponent).ToList(),
        }).ToList(),
    };

    public static IConditionComponentNode? CloneCondition(IConditionComponentNode? node) => node switch
    {
        LogicalGroupNode g => new LogicalGroupNode
        {
            IsAnd = g.IsAnd,
            Children = g.Children.Select(CloneCondition)
                                 .Where(c => c is not null)
                                 .Cast<IConditionComponentNode>()
                                 .ToList(),
        },
        AtomicConditionNode a => new AtomicConditionNode
        {
            EntityId = a.EntityId,
            ComponentId = a.ComponentId,
            Operator = a.Operator,
            TargetValue = a.TargetValue,
        },
        _ => null,
    };

    public static CommandSnapshot CloneCommand(CommandSnapshot cmd)
    {
        CommandSnapshot clone = cmd switch
        {
            ModifyNumberCommandSnapshot n => new ModifyNumberCommandSnapshot(n.TargetEntityId, n.TargetComponentName, n.Field, n.Value),
            ModifyTextCommandSnapshot t => new ModifyTextCommandSnapshot(t.TargetEntityId, t.TargetComponentName, t.Value),
            ModifyBooleanCommandSnapshot b => new ModifyBooleanCommandSnapshot(b.TargetEntityId, b.TargetComponentName, b.Value),
            _ => cmd
        };
        clone.Condition = CloneCondition(cmd.Condition);
        return clone;
    }

    public static ComponentSnapshot CloneComponent(ComponentSnapshot comp)
    {
        ComponentSnapshot clone = comp switch
        {
            NumberComponentSnapshot n => new NumberComponentSnapshot(n.Name, n.Value, n.MinValue, n.MaxValue),
            TextComponentSnapshot t => new TextComponentSnapshot(t.Name, t.Value),
            ConditionComponentSnapshot c => new ConditionComponentSnapshot(c.Name, c.Value),
            _ => comp
        };
        clone.Visible = comp.Visible;
        return clone;
    }
}
