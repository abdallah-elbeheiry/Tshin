using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Tshin.Models;

/// <summary>
/// Lightweight, UI-facing description of a project as shown in the sidebar.
/// The real persistence layer (owned by another dev) will produce these;
/// for the ui-rework branch they come from <see cref="Tshin.Services.MockProjectService"/>.
/// </summary>
public sealed partial class ProjectSummary : ObservableObject
{
    public required string Id { get; init; }

    /// <summary>Editable epic title. Observable so the sidebar reflects renames live.</summary>
    [ObservableProperty]
    private string _name = "";

    public string? Description { get; init; }
    public int NodeCount { get; init; }
    public DateTimeOffset LastModified { get; init; }

    /// <summary>True when the project originated from an imported (downloaded) file.</summary>
    public bool IsImported { get; init; }
    public string? FilePath { get; set; }
}
