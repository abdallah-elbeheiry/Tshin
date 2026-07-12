using System;
using System.Collections.Generic;
using System.Linq;
using Tshin.Core.Models;
using Tshin.Core.Utils.Commands;
using Tshin.Core.Utils.Factories;
using Tshin.Core.Utils.Managers;
using Tshin.Models;

namespace Tshin.Services;

/// <summary>
/// Translates between the UI-facing <see cref="StorySnapshot"/> and the domain
/// <see cref="EntityManager"/>/<see cref="NodeManager"/> object graph that
/// <see cref="Tshin.Core.Utils.Systems.FileReader"/>/<c>FileWriter</c> read and write.
/// Shared by every <see cref="IProjectService"/> implementation.
/// </summary>
public static class StoryMapper
{
    /// <summary>Reads a populated <paramref name="em"/>/<paramref name="nm"/> into a snapshot.</summary>
    public static StorySnapshot ToSnapshot(EntityManager em, NodeManager nm, string projectId)
    {
        var nodes = nm.GetNodes().Select(n =>
        {
            var choices = new List<ChoiceSnapshot>();
            if (n is IBranchingNode branchingNode)
            {
                foreach (var c in branchingNode.Choices)
                {
                    var cmdSnapshots = c.Commands
                        .Select(CommandFromDomain)
                        .Where(cmd => cmd is not null)
                        .Cast<CommandSnapshot>()
                        .ToList();
                    choices.Add(new ChoiceSnapshot
                    {
                        DisplayText = c.DisplayText,
                        TargetNodeId = c.Node?.Id,
                        Condition = c.Condition,
                        ConditionFalseBehavior = c.ConditionFalseBehavior,
                        Commands = cmdSnapshots
                    });
                }
            }
            return new NodeSnapshot
            {
                Id = n.Id,
                DisplayText = n.DisplayText,
                X = n.X,
                Y = n.Y,
                Choices = choices
            };
        }).ToList();

        var entities = em.GetAllEntities().Select(e =>
        {
            var es = new EntitySnapshot
            {
                Id = e.Id.ToString(),
                Name = e.Name,
                X = e.X,
                Y = e.Y,
                Visible = e.Visible,
            };
            foreach (var comp in em.GetComponentsForEntity(e))
            {
                ComponentSnapshot? cs = comp switch
                {
                    NumberComponent n => new NumberComponentSnapshot(n.Name, n.Value, n.MinValue, n.MaxValue),
                    TextComponent t => new TextComponentSnapshot(t.Name, t.Value),
                    ConditionComponent c => new ConditionComponentSnapshot(c.Name, c.Value),
                    _ => null
                };
                if (cs is not null)
                {
                    cs.Visible = comp.Visible;
                    es.Components.Add(cs);
                }
            }
            return es;
        }).ToList();

        return new StorySnapshot
        {
            ProjectId = projectId,
            Nodes = nodes,
            Entities = entities
        };
    }

    /// <summary>Clears <paramref name="em"/>/<paramref name="nm"/> and repopulates them from a snapshot.</summary>
    public static void ToManagers(StorySnapshot snapshot, EntityManager em, NodeManager nm)
    {
        nm.ClearNodes();
        em.ClearEntities();

        // Populate EntityManager from snapshot
        foreach (var es in snapshot.Entities)
        {
            var entity = em.CreateEntity(Guid.Parse(es.Id));
            entity.X = es.X;
            entity.Y = es.Y;
            entity.Name = es.Name;
            entity.Visible = es.Visible;
            foreach (var cs in es.Components)
            {
                switch (cs)
                {
                    case NumberComponentSnapshot n:
                        em.SetComponent(entity, new NumberComponent
                        {
                            Name = n.Name, Value = n.Value, MinValue = n.MinValue, MaxValue = n.MaxValue, Visible = n.Visible
                        });
                        break;
                    case TextComponentSnapshot t:
                        em.SetComponent(entity, new TextComponent
                        {
                            Name = t.Name, Value = t.Value, Visible = t.Visible
                        });
                        break;
                    case ConditionComponentSnapshot c:
                        em.SetComponent(entity, new ConditionComponent
                        {
                            Name = c.Name, Value = c.Value, Visible = c.Visible
                        });
                        break;
                }
            }
        }

        // Populate NodeManager from snapshot
        foreach (var ns in snapshot.Nodes)
        {
            var node = NodeFactory.CreateNode(NodeType.StoryNode, ns.Id, ns.X, ns.Y);
            node.DisplayText = ns.DisplayText;
            nm.AppendNode(node);
        }

        // Link choices
        foreach (var ns in snapshot.Nodes)
        {
            if (nm.TryGetNode(ns.Id, out var node) && node is IBranchingNode branchingNode)
            {
                foreach (var cs in ns.Choices)
                {
                    INode? target = null;
                    if (cs.TargetNodeId != null)
                    {
                        nm.TryGetNode(cs.TargetNodeId, out target);
                    }
                    var choice = new Choice(target, cs.DisplayText)
                    {
                        Condition = cs.Condition,
                        ConditionFalseBehavior = cs.ConditionFalseBehavior,
                    };

                    foreach (var cmd in cs.Commands)
                    {
                        var domainCmd = CommandFromSnapshot(cmd, em);
                        if (domainCmd is not null)
                            choice.Commands.Add(domainCmd);
                    }

                    branchingNode.Choices.Add(choice);
                }
            }
        }
    }

    public static CommandSnapshot? CommandFromDomain(ICommand cmd)
    {
        return cmd switch
        {
            ModifyNumberCommand n => new ModifyNumberCommandSnapshot(
                n.Entity.Id.ToString(), n.TargetComponentName, n.Field.ToString(), n.Value),
            ModifyTextCommand t => new ModifyTextCommandSnapshot(
                t.Entity.Id.ToString(), t.TargetComponentName, t.Value),
            ModifyBooleanCommand b => new ModifyBooleanCommandSnapshot(
                b.Entity.Id.ToString(), b.TargetComponentName, b.Value),
            _ => null
        };
    }

    public static ICommand? CommandFromSnapshot(CommandSnapshot cmd, EntityManager entityManager)
    {
        var entity = entityManager.GetAllEntities()
            .FirstOrDefault(e => e.Id.ToString() == cmd.TargetEntityId);
        if (entity is null) return null;

        return cmd switch
        {
            ModifyNumberCommandSnapshot n => new ModifyNumberCommand
            {
                Entity = entity,
                TargetComponentName = n.TargetComponentName,
                Value = n.Value,
                Field = Enum.TryParse<CommandField>(n.Field, true, out var f) ? f : CommandField.Set
            },
            ModifyTextCommandSnapshot t => new ModifyTextCommand
            {
                Entity = entity,
                TargetComponentName = t.TargetComponentName,
                Value = t.Value,
                Field = CommandField.Set
            },
            ModifyBooleanCommandSnapshot b => new ModifyBooleanCommand
            {
                Entity = entity,
                TargetComponentName = b.TargetComponentName,
                Value = b.Value,
                Field = CommandField.Set
            },
            _ => null
        };
    }
}
