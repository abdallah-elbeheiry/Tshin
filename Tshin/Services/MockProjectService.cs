using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Tshin.Core.Models;
using Tshin.Core.Utils.Commands;
using Tshin.Core.Utils.Factories;
using Tshin.Core.Utils.Managers;
using Tshin.Core.Utils.Systems;
using Tshin.Models;

namespace Tshin.Services;

/// <summary>
/// In-memory stand-in for the real persistence layer. Starts empty; projects appear
/// as the user creates or imports them. Nothing touches disk except
/// <see cref="ImportProjectAsync"/> and <see cref="ExportProjectAsync"/>, which use
/// <see cref="FileReader"/> and <see cref="FileWriter"/>.
/// </summary>
public sealed class MockProjectService : IProjectService
{
    private readonly Dictionary<string, ProjectSummary> _projects = new();
    private readonly Dictionary<string, StorySnapshot> _stories = new();
    private readonly EntityManager _entityManager = new();
    private readonly NodeManager _nodeManager = new();

    public Task<IReadOnlyList<ProjectSummary>> GetProjectsAsync()
        => Task.FromResult<IReadOnlyList<ProjectSummary>>(
            _projects.Values.OrderByDescending(p => p.LastModified).ToList());

    public Task<StorySnapshot> OpenProjectAsync(string projectId)
    {
        if (!_stories.TryGetValue(projectId, out var story))
            throw new KeyNotFoundException($"No project with id '{projectId}'.");

        // Hand back a deep copy so the editor mutates its own graph until it saves.
        return Task.FromResult(Clone(story));
    }

    public Task<ProjectSummary> CreateProjectAsync(string name)
    {
        var id = Guid.NewGuid().ToString("N");
        var start = new NodeSnapshot { Id = "start", DisplayText = "Once upon a time…", X = 120, Y = 120 };
        var summary = new ProjectSummary
        {
            Id = id,
            Name = string.IsNullOrWhiteSpace(name) ? "Untitled Epic" : name.Trim(),
            Description = "A fresh story.",
            NodeCount = 1,
            LastModified = DateTimeOffset.Now,
        };
        _projects[id] = summary;
        _stories[id] = new StorySnapshot { ProjectId = id, Nodes = { start } };
        return Task.FromResult(summary);
    }

    public async Task<ProjectSummary> ImportProjectAsync(string filePath)
    {
        await FileReader.LoadFileAsync(filePath, _entityManager, _nodeManager);

        var id = Guid.NewGuid().ToString("N");
        var name = Path.GetFileNameWithoutExtension(filePath);
        
        var nodes = _nodeManager.GetNodes().Select(n =>
        {
            var choices = new List<ChoiceSnapshot>();
            if (n is IBranchingNode branchingNode)
            {
                foreach (var c in branchingNode.Choices)
                {
                    var cmdSnapshots = c.Commands
                        .Select(cmd => CommandFromDomain(cmd))
                        .Where(cmd => cmd is not null)
                        .Cast<CommandSnapshot>()
                        .ToList();
                    choices.Add(new ChoiceSnapshot
                    {
                        DisplayText = c.DisplayText,
                        TargetNodeId = c.Node?.Id,
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

        var entities = _entityManager.GetAllEntities().Select(e =>
        {
            var es = new EntitySnapshot
            {
                Id = e.Id.ToString(),
                Name = e.Name,
                X = e.X,
                Y = e.Y,
            };
            foreach (var comp in _entityManager.GetComponentsForEntity(e))
            {
                ComponentSnapshot? cs = comp switch
                {
                    NumberComponent n => new NumberComponentSnapshot(n.Name, n.Value, n.MinValue, n.MaxValue),
                    TextComponent t => new TextComponentSnapshot(t.Name, t.Value),
                    ConditionComponent c => new ConditionComponentSnapshot(c.Name, c.Value),
                    _ => null
                };
                if (cs is not null)
                    es.Components.Add(cs);
            }
            return es;
        }).ToList();

        var summary = new ProjectSummary
        {
            Id = id,
            Name = string.IsNullOrWhiteSpace(name) ? "Imported Epic" : name,
            Description = $"Imported from {Path.GetFileName(filePath)}",
            NodeCount = nodes.Count,
            LastModified = DateTimeOffset.Now,
            IsImported = true,
            FilePath = filePath,
        };

        _projects[id] = summary;
        _stories[id] = new StorySnapshot
        {
            ProjectId = id,
            Nodes = nodes,
            Entities = entities
        };

        return summary;
    }

    public async Task ExportProjectAsync(StorySnapshot snapshot, string filePath)
    {
        if (_projects.TryGetValue(snapshot.ProjectId, out var summary))
        {
            summary.FilePath = filePath;
        }

        _nodeManager.ClearNodes();
        _entityManager.ClearEntities();
        
        // Populate EntityManager from snapshot
        foreach (var es in snapshot.Entities)
        {
            var entity = _entityManager.CreateEntity(Guid.Parse(es.Id));
            entity.X = es.X;
            entity.Y = es.Y;
            entity.Name = es.Name;
            foreach (var cs in es.Components)
            {
                switch (cs)
                {
                    case NumberComponentSnapshot n:
                        _entityManager.SetComponent(entity, new NumberComponent
                        {
                            Name = n.Name, Value = n.Value, MinValue = n.MinValue, MaxValue = n.MaxValue
                        });
                        break;
                    case TextComponentSnapshot t:
                        _entityManager.SetComponent(entity, new TextComponent
                        {
                            Name = t.Name, Value = t.Value
                        });
                        break;
                    case ConditionComponentSnapshot c:
                        _entityManager.SetComponent(entity, new ConditionComponent
                        {
                            Name = c.Name, Value = c.Value
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
            _nodeManager.AppendNode(node);
        }

        // Link choices
        foreach (var ns in snapshot.Nodes)
        {
            if (_nodeManager.TryGetNode(ns.Id, out var node) && node is IBranchingNode branchingNode)
            {
                foreach (var cs in ns.Choices)
                {
                    INode? target = null;
                    if (cs.TargetNodeId != null)
                    {
                        _nodeManager.TryGetNode(cs.TargetNodeId, out target);
                    }
                    var choice = new Choice(target, cs.DisplayText);
                    
                    // Build commands from snapshot
                    foreach (var cmd in cs.Commands)
                    {
                        var domainCmd = CommandFromSnapshot(cmd, _entityManager);
                        if (domainCmd is not null)
                            choice.Commands.Add(domainCmd);
                    }
                    
                    branchingNode.Choices.Add(choice);
                }
            }
        }

        await FileWriter.SaveFileAsync(filePath, _entityManager, _nodeManager);
    }

    public async Task SaveProjectAsync(StorySnapshot snapshot)
    {
        _stories[snapshot.ProjectId] = Clone(snapshot);
        if (_projects.TryGetValue(snapshot.ProjectId, out var existing))
        {
            _projects[snapshot.ProjectId] = new ProjectSummary
            {
                Id = existing.Id,
                Name = existing.Name,
                Description = existing.Description,
                NodeCount = snapshot.Nodes.Count,
                LastModified = DateTimeOffset.Now,
                IsImported = existing.IsImported,
                FilePath = existing.FilePath,
            };

            if (!string.IsNullOrEmpty(existing.FilePath))
            {
                await ExportProjectAsync(snapshot, existing.FilePath);
            }
        }
    }

    public Task<string?> GetProjectFilePathAsync(string projectId)
    {
        if (_projects.TryGetValue(projectId, out var summary))
        {
            return Task.FromResult(summary.FilePath);
        }
        return Task.FromResult<string?>(null);
    }

    private static StorySnapshot Clone(StorySnapshot source) => new()
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
                Commands = c.Commands.Select(CloneCommand).ToList(),
            }).ToList(),
        }).ToList(),
        Entities = source.Entities.Select(e => new EntitySnapshot
        {
            Id = e.Id,
            Name = e.Name,
            X = e.X,
            Y = e.Y,
            Components = e.Components.Select(CloneComponent).ToList(),
        }).ToList(),
    };

    private static CommandSnapshot CloneCommand(CommandSnapshot cmd) => cmd switch
    {
        ModifyNumberCommandSnapshot n => new ModifyNumberCommandSnapshot(n.TargetEntityId, n.TargetComponentName, n.Field, n.Value),
        ModifyTextCommandSnapshot t => new ModifyTextCommandSnapshot(t.TargetEntityId, t.TargetComponentName, t.Value),
        ModifyBooleanCommandSnapshot b => new ModifyBooleanCommandSnapshot(b.TargetEntityId, b.TargetComponentName, b.Value),
        _ => cmd
    };

    private static ComponentSnapshot CloneComponent(ComponentSnapshot comp) => comp switch
    {
        NumberComponentSnapshot n => new NumberComponentSnapshot(n.Name, n.Value, n.MinValue, n.MaxValue),
        TextComponentSnapshot t => new TextComponentSnapshot(t.Name, t.Value),
        ConditionComponentSnapshot c => new ConditionComponentSnapshot(c.Name, c.Value),
        _ => comp
    };

    private static CommandSnapshot? CommandFromDomain(ICommand cmd)
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

    private static ICommand? CommandFromSnapshot(CommandSnapshot cmd, EntityManager entityManager)
    {
        // Resolve the entity by ID
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