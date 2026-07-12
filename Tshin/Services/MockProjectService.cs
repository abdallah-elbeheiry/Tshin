using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Tshin.Core.Utils.Managers;
using Tshin.Core.Utils.Systems;
using Tshin.Models;

namespace Tshin.Services;

/// <summary>
/// In-memory stand-in for the real persistence layer, used by tests and design-time.
/// Starts empty; projects appear as the user creates or imports them. Nothing touches
/// disk except <see cref="ImportProjectAsync"/> and <see cref="ExportProjectAsync"/>,
/// which use <see cref="FileReader"/> and <see cref="FileWriter"/> via <see cref="StoryMapper"/>.
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
        return Task.FromResult(SnapshotCloner.Clone(story));
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
        var story = StoryMapper.ToSnapshot(_entityManager, _nodeManager, id);

        var summary = new ProjectSummary
        {
            Id = id,
            Name = string.IsNullOrWhiteSpace(name) ? "Imported Epic" : name,
            Description = $"Imported from {Path.GetFileName(filePath)}",
            NodeCount = story.Nodes.Count,
            LastModified = DateTimeOffset.Now,
            IsImported = true,
            FilePath = filePath,
        };

        _projects[id] = summary;
        _stories[id] = story;

        return summary;
    }

    public async Task ExportProjectAsync(StorySnapshot snapshot, string filePath)
    {
        if (_projects.TryGetValue(snapshot.ProjectId, out var summary))
        {
            summary.FilePath = filePath;
        }

        StoryMapper.ToManagers(snapshot, _entityManager, _nodeManager);
        await FileWriter.SaveFileAsync(filePath, _entityManager, _nodeManager);
    }

    public async Task SaveProjectAsync(StorySnapshot snapshot)
    {
        _stories[snapshot.ProjectId] = SnapshotCloner.Clone(snapshot);
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
}
