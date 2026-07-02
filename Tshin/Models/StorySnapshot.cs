using System;
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
    public List<EntitySnapshot> Entities { get; init; } = new();
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
    
    public List<CommandSnapshot> Commands { get; init; } = new();
}

// ---- Polymorphic entity / component / command snapshots ----

public sealed class EntitySnapshot
{
    public required string Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public List<ComponentSnapshot> Components { get; init; } = new();
}

/// <summary>Polymorphic base — mirrors <c>IComponent</c>.</summary>
public abstract record ComponentSnapshot(string Name);

public sealed record NumberComponentSnapshot(string Name, double Value, double MinValue, double MaxValue)
    : ComponentSnapshot(Name);

public sealed record TextComponentSnapshot(string Name, string Value)
    : ComponentSnapshot(Name);

public sealed record ConditionComponentSnapshot(string Name, bool Value)
    : ComponentSnapshot(Name);

/// <summary>Polymorphic base — mirrors <c>ICommand</c>.</summary>
public abstract record CommandSnapshot(string TargetEntityId, string TargetComponentName, string Field);

public sealed record ModifyNumberCommandSnapshot(string TargetEntityId, string TargetComponentName, string Field, double Value)
    : CommandSnapshot(TargetEntityId, TargetComponentName, Field);

public sealed record ModifyTextCommandSnapshot(string TargetEntityId, string TargetComponentName, string Value)
    : CommandSnapshot(TargetEntityId, TargetComponentName, "Set");

public sealed record ModifyBooleanCommandSnapshot(string TargetEntityId, string TargetComponentName, bool Value)
    : CommandSnapshot(TargetEntityId, TargetComponentName, "Set");