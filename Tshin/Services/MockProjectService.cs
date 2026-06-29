using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Tshin.Core.Models;
using Tshin.Core.Utils;
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
        await FileReader.LoadFileAsync(filePath, _entityManager);

        var id = Guid.NewGuid().ToString("N");
        var name = Path.GetFileNameWithoutExtension(filePath);
        
        var nodes = NodeManager.GetNodes().Select(n => new NodeSnapshot
        {
            Id = n.Id,
            DisplayText = n.DisplayText,
            X = n.X,
            Y = n.Y,
            Choices = (n as IBranchingNode)?.Choices.Select(c => new ChoiceSnapshot
            {
                DisplayText = c.DisplayText,
                TargetNodeId = c.Node?.Id
            }).ToList() ?? new List<ChoiceSnapshot>()
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
            Nodes = nodes
        };

        return summary;
    }

    public async Task ExportProjectAsync(StorySnapshot snapshot, string filePath)
    {
        if (_projects.TryGetValue(snapshot.ProjectId, out var summary))
        {
            summary.FilePath = filePath;
        }

        NodeManager.ClearNodes();
        
        // Populate NodeManager from snapshot
        foreach (var ns in snapshot.Nodes)
        {
            var node = NodeFactory.CreateNode(NodeType.StoryNode, ns.Id, ns.X, ns.Y);
            node.DisplayText = ns.DisplayText;
        }

        // Link choices
        foreach (var ns in snapshot.Nodes)
        {
            if (NodeManager.TryGetNode(ns.Id, out var node) && node is IBranchingNode branchingNode)
            {
                foreach (var cs in ns.Choices)
                {
                    INode? target = null;
                    if (cs.TargetNodeId != null)
                    {
                        NodeManager.TryGetNode(cs.TargetNodeId, out target);
                    }
                    var choice = new Choice(target, cs.DisplayText);
                    branchingNode.Choices.Add(choice);
                }
            }
        }

        await FileWriter.SaveFileAsync(filePath, _entityManager);
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
            }).ToList(),
        }).ToList(),
    };
}
