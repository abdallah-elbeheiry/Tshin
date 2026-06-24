using System.Collections.Generic;

namespace Tshin.Models;

/// <summary>
/// A plain, serialization-friendly snapshot of a story graph handed across the
/// UI/persistence boundary. The editor view models are built from this and write
/// back into it on save. Node layout (<see cref="NodeSnapshot.X"/>/<see cref="NodeSnapshot.Y"/>)
/// lives here purely for the UI; the real domain layer may choose to persist or
/// recompute it however it likes.
/// </summary>
public sealed class StorySnapshot
{
    public required string ProjectId { get; init; }
    public List<NodeSnapshot> Nodes { get; init; } = new();
}

public sealed class NodeSnapshot
{
    public required string Id { get; set; }
    public string DisplayText { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public List<ChoiceSnapshot> Choices { get; init; } = new();
}

public sealed class ChoiceSnapshot
{
    public string DisplayText { get; set; } = string.Empty;

    /// <summary>Id of the target <see cref="NodeSnapshot"/>, or null if unlinked.</summary>
    public string? TargetNodeId { get; set; }
}
