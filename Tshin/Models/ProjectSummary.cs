using System;

namespace Tshin.Models;

/// <summary>
/// Lightweight, UI-facing description of a project as shown in the sidebar.
/// The real persistence layer (owned by another dev) will produce these;
/// for the ui-rework branch they come from <see cref="Tshin.Services.MockProjectService"/>.
/// </summary>
public sealed class ProjectSummary
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public int NodeCount { get; init; }
    public DateTimeOffset LastModified { get; init; }

    /// <summary>True when the project originated from an imported (downloaded) file.</summary>
    public bool IsImported { get; init; }
    public string? FilePath { get; set; }
}
