using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Tshin.Models;
using Tshin.Services;
using Xunit;

namespace Tshin.Tests;

/// <summary>
/// Round-trips through the filesystem-backed <see cref="FileProjectService"/> against a temp
/// directory: create/save/open/export/import and cross-instance persistence via index.json.
/// </summary>
public class FileProjectServiceTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"tshin_svc_{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Created_project_is_listed_and_opens_with_a_start_node()
    {
        var service = new FileProjectService(_root);
        var summary = await service.CreateProjectAsync("My Epic");

        var projects = await service.GetProjectsAsync();
        Assert.Contains(projects, p => p.Id == summary.Id && p.Name == "My Epic");

        var story = await service.OpenProjectAsync(summary.Id);
        Assert.Single(story.Nodes);
        Assert.Equal("start", story.Nodes[0].Id);
    }

    [Fact]
    public async Task Save_persists_edits_that_reopen_reflects()
    {
        var service = new FileProjectService(_root);
        var summary = await service.CreateProjectAsync("Editable");

        var story = await service.OpenProjectAsync(summary.Id);
        story.Nodes[0].DisplayText = "Changed text";
        story.Nodes.Add(new NodeSnapshot { Id = "second", DisplayText = "Second" });
        await service.SaveProjectAsync(story);

        var reopened = await service.OpenProjectAsync(summary.Id);
        Assert.Equal(2, reopened.Nodes.Count);
        Assert.Contains(reopened.Nodes, n => n.Id == "start" && n.DisplayText == "Changed text");
        Assert.Contains(reopened.Nodes, n => n.Id == "second");
    }

    [Fact]
    public async Task Index_persists_across_service_instances()
    {
        var id = (await new FileProjectService(_root).CreateProjectAsync("Persisted")).Id;

        // A fresh service over the same root reconstructs the catalogue from index.json.
        var reloaded = new FileProjectService(_root);
        var projects = await reloaded.GetProjectsAsync();
        Assert.Contains(projects, p => p.Id == id && p.Name == "Persisted");

        var story = await reloaded.OpenProjectAsync(id);
        Assert.Equal("start", story.Nodes.Single().Id);
    }

    [Fact]
    public async Task Export_then_import_round_trips_through_an_external_file()
    {
        var service = new FileProjectService(_root);
        var summary = await service.CreateProjectAsync("Exported");
        var story = await service.OpenProjectAsync(summary.Id);
        story.Nodes[0].DisplayText = "Exported body";

        var externalPath = Path.Combine(_root, "exported.tshin");
        await service.ExportProjectAsync(story, externalPath);
        Assert.True(File.Exists(externalPath));

        var imported = await service.ImportProjectAsync(externalPath);
        Assert.True(imported.IsImported);
        Assert.Equal(externalPath, imported.FilePath);

        var importedStory = await service.OpenProjectAsync(imported.Id);
        Assert.Contains(importedStory.Nodes, n => n.DisplayText == "Exported body");
    }

    [Fact]
    public async Task GetProjectFilePath_returns_null_until_exported()
    {
        var service = new FileProjectService(_root);
        var summary = await service.CreateProjectAsync("PathProbe");
        Assert.Null(await service.GetProjectFilePathAsync(summary.Id));

        var story = await service.OpenProjectAsync(summary.Id);
        var externalPath = Path.Combine(_root, "probe.tshin");
        await service.ExportProjectAsync(story, externalPath);

        Assert.Equal(externalPath, await service.GetProjectFilePathAsync(summary.Id));
    }
}
