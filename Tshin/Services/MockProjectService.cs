using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Tshin.Models;

namespace Tshin.Services;

/// <summary>
/// In-memory stand-in for the real persistence layer. Starts empty; projects appear
/// as the user creates or imports them. Nothing touches disk except
/// <see cref="ImportProjectAsync"/>, which only reads the dropped file's name.
/// </summary>
public sealed class MockProjectService : IProjectService
{
    private readonly Dictionary<string, ProjectSummary> _projects = new();
    private readonly Dictionary<string, StorySnapshot> _stories = new();

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

    public Task<ProjectSummary> ImportProjectAsync(string filePath)
    {
        var id = Guid.NewGuid().ToString("N");
        var name = Path.GetFileNameWithoutExtension(filePath);
        var summary = new ProjectSummary
        {
            Id = id,
            Name = string.IsNullOrWhiteSpace(name) ? "Imported Epic" : name,
            Description = $"Imported from {Path.GetFileName(filePath)}",
            NodeCount = 1,
            LastModified = DateTimeOffset.Now,
            IsImported = true,
        };
        _projects[id] = summary;
        _stories[id] = new StorySnapshot
        {
            ProjectId = id,
            Nodes = { new NodeSnapshot { Id = "start", DisplayText = $"(Imported placeholder for '{name}')", X = 120, Y = 120 } },
        };
        return Task.FromResult(summary);
    }

    public Task SaveProjectAsync(StorySnapshot snapshot)
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
            };
        }
        return Task.CompletedTask;
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
