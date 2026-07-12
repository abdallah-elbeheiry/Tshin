using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Tshin.Core.Utils.Managers;
using Tshin.Core.Utils.Systems;
using Tshin.Models;

namespace Tshin.Services;

/// <summary>
/// Filesystem-backed project service. Each project is stored as a <c>.tshin</c> file at
/// <c>{root}/{id}.tshin</c>; an <c>index.json</c> sidecar carries the project metadata the
/// <c>.tshin</c> format has no room for (name, description, modified time, import origin).
/// The storage root is injected so tests can point it at a temp directory.
/// </summary>
public sealed class FileProjectService : IProjectService
{
    private readonly string _root;
    private readonly string _indexPath;
    private readonly Dictionary<string, IndexEntry> _index;
    private readonly EntityManager _entityManager = new();
    private readonly NodeManager _nodeManager = new();

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public FileProjectService(string storageRoot)
    {
        _root = storageRoot;
        _indexPath = Path.Combine(_root, "index.json");
        _index = LoadIndex();
    }

    public Task<IReadOnlyList<ProjectSummary>> GetProjectsAsync()
        => Task.FromResult<IReadOnlyList<ProjectSummary>>(
            _index.Values.OrderByDescending(e => e.LastModified).Select(ToSummary).ToList());

    public async Task<StorySnapshot> OpenProjectAsync(string projectId)
    {
        if (!_index.ContainsKey(projectId))
            throw new KeyNotFoundException($"No project with id '{projectId}'.");

        await FileReader.LoadFileAsync(ProjectPath(projectId), _entityManager, _nodeManager);
        return StoryMapper.ToSnapshot(_entityManager, _nodeManager, projectId);
    }

    public async Task<ProjectSummary> CreateProjectAsync(string name)
    {
        var id = Guid.NewGuid().ToString("N");
        var start = new NodeSnapshot { Id = "start", DisplayText = "Once upon a time…", X = 120, Y = 120 };
        var story = new StorySnapshot { ProjectId = id, Nodes = { start } };

        await WriteStoryAsync(story, ProjectPath(id));

        var entry = new IndexEntry
        {
            Id = id,
            Name = string.IsNullOrWhiteSpace(name) ? "Untitled Epic" : name.Trim(),
            Description = "A fresh story.",
            NodeCount = 1,
            LastModified = DateTimeOffset.Now,
        };
        _index[id] = entry;
        SaveIndex();
        return ToSummary(entry);
    }

    public async Task<ProjectSummary> ImportProjectAsync(string filePath)
    {
        await FileReader.LoadFileAsync(filePath, _entityManager, _nodeManager);

        var id = Guid.NewGuid().ToString("N");
        var name = Path.GetFileNameWithoutExtension(filePath);
        var story = StoryMapper.ToSnapshot(_entityManager, _nodeManager, id);

        // Keep an internal copy so OpenProjectAsync reads uniformly from the store.
        EnsureRoot();
        await FileWriter.SaveFileAsync(ProjectPath(id), _entityManager, _nodeManager);

        var entry = new IndexEntry
        {
            Id = id,
            Name = string.IsNullOrWhiteSpace(name) ? "Imported Epic" : name,
            Description = $"Imported from {Path.GetFileName(filePath)}",
            NodeCount = story.Nodes.Count,
            LastModified = DateTimeOffset.Now,
            IsImported = true,
            FilePath = filePath,
        };
        _index[id] = entry;
        SaveIndex();
        return ToSummary(entry);
    }

    public async Task ExportProjectAsync(StorySnapshot snapshot, string filePath)
    {
        await WriteStoryAsync(snapshot, filePath);

        if (_index.TryGetValue(snapshot.ProjectId, out var entry))
        {
            entry.FilePath = filePath;
            SaveIndex();
        }
    }

    public async Task SaveProjectAsync(StorySnapshot snapshot)
    {
        await WriteStoryAsync(snapshot, ProjectPath(snapshot.ProjectId));

        if (_index.TryGetValue(snapshot.ProjectId, out var entry))
        {
            entry.NodeCount = snapshot.Nodes.Count;
            entry.LastModified = DateTimeOffset.Now;
            SaveIndex();

            // Mirror to the external file the project was exported to, if any.
            if (!string.IsNullOrEmpty(entry.FilePath))
                await WriteStoryAsync(snapshot, entry.FilePath);
        }
    }

    public Task<string?> GetProjectFilePathAsync(string projectId)
        => Task.FromResult(_index.TryGetValue(projectId, out var entry) ? entry.FilePath : null);

    // ---- helpers ------------------------------------------------------------

    private async Task WriteStoryAsync(StorySnapshot snapshot, string filePath)
    {
        EnsureRoot();
        StoryMapper.ToManagers(snapshot, _entityManager, _nodeManager);
        await FileWriter.SaveFileAsync(filePath, _entityManager, _nodeManager);
    }

    private string ProjectPath(string id) => Path.Combine(_root, id + ".tshin");

    private void EnsureRoot() => Directory.CreateDirectory(_root);

    private Dictionary<string, IndexEntry> LoadIndex()
    {
        if (!File.Exists(_indexPath)) return new Dictionary<string, IndexEntry>();
        try
        {
            var json = File.ReadAllText(_indexPath);
            var entries = JsonSerializer.Deserialize<List<IndexEntry>>(json) ?? new List<IndexEntry>();
            return entries.Where(e => !string.IsNullOrEmpty(e.Id)).ToDictionary(e => e.Id);
        }
        catch
        {
            // A corrupt index shouldn't crash startup; start from an empty catalogue.
            return new Dictionary<string, IndexEntry>();
        }
    }

    private void SaveIndex()
    {
        EnsureRoot();
        var json = JsonSerializer.Serialize(_index.Values.ToList(), JsonOptions);
        File.WriteAllText(_indexPath, json);
    }

    private static ProjectSummary ToSummary(IndexEntry e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        Description = e.Description,
        NodeCount = e.NodeCount,
        LastModified = e.LastModified,
        IsImported = e.IsImported,
        FilePath = e.FilePath,
    };

    private sealed class IndexEntry
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int NodeCount { get; set; }
        public DateTimeOffset LastModified { get; set; }
        public bool IsImported { get; set; }
        public string? FilePath { get; set; }
    }
}
