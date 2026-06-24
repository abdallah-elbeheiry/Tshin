using System.Collections.Generic;
using System.Threading.Tasks;
using Tshin.Models;

namespace Tshin.Services;

/// <summary>
/// Contract the UI depends on for listing, opening, creating, importing and saving
/// projects. On the ui-rework branch this is fulfilled by <see cref="MockProjectService"/>;
/// a real disk/file-format backed implementation will replace it later without UI changes.
/// </summary>
public interface IProjectService
{
    Task<IReadOnlyList<ProjectSummary>> GetProjectsAsync();

    Task<StorySnapshot> OpenProjectAsync(string projectId);

    Task<ProjectSummary> CreateProjectAsync(string name);

    /// <summary>
    /// Imports a project from a file the user downloaded from the web
    /// (file picker or drag-and-drop). Returns the new project's summary.
    /// </summary>
    Task<ProjectSummary> ImportProjectAsync(string filePath);

    Task SaveProjectAsync(StorySnapshot snapshot);
}
