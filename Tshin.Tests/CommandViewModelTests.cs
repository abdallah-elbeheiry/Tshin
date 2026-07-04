using System.Linq;
using Avalonia.Headless.XUnit;
using Tshin.ViewModels;
using Xunit;

namespace Tshin.Tests;

/// <summary>Bug 8: the command's component picker must track the entity's live components.</summary>
public class CommandViewModelTests
{
    [AvaloniaFact]
    public void Adding_a_component_after_selection_refreshes_available_components()
    {
        var editor = TestFactory.Editor();
        var node = editor.CreateNodeAt(0, 0);
        var entity = editor.CreateEntityAt(0, 100);
        editor.AddComponentToEntity(entity, "text");   // 1 component
        editor.AddComponentToEntity(entity, "number"); // 2 components

        var choice = TestFactory.AddChoice(editor, node);
        var cmd = TestFactory.AddCommand(editor, choice);
        cmd.TargetEntity = entity;
        Assert.Equal(2, cmd.AvailableComponents.Count);

        // Repro from the bug report: add a component *after* the command picked the entity.
        editor.AddComponentToEntity(entity, "condition");

        Assert.Equal(3, cmd.AvailableComponents.Count);
    }

    [AvaloniaFact]
    public void Refresh_preserves_the_current_component_selection()
    {
        var editor = TestFactory.Editor();
        var node = editor.CreateNodeAt(0, 0);
        var entity = editor.CreateEntityAt(0, 100);
        editor.AddComponentToEntity(entity, "number");
        var number = entity.Components[0];

        var choice = TestFactory.AddChoice(editor, node);
        var cmd = TestFactory.AddCommand(editor, choice);
        cmd.TargetEntity = entity;
        cmd.SelectedComponent = cmd.AvailableComponents.First();

        editor.AddComponentToEntity(entity, "text");

        Assert.NotNull(cmd.SelectedComponent);
        Assert.Equal(number.Name, cmd.SelectedComponent!.Name);
    }

    [AvaloniaFact]
    public void Switching_target_entity_stops_tracking_the_old_one()
    {
        var editor = TestFactory.Editor();
        var node = editor.CreateNodeAt(0, 0);
        var a = editor.CreateEntityAt(0, 100);
        var b = editor.CreateEntityAt(0, 200);
        editor.AddComponentToEntity(a, "number");

        var choice = TestFactory.AddChoice(editor, node);
        var cmd = TestFactory.AddCommand(editor, choice);
        cmd.TargetEntity = a;
        cmd.TargetEntity = b; // switch away from a

        editor.AddComponentToEntity(a, "text"); // change on the *old* entity

        // b has no components; the stale subscription to a must not leak back in.
        Assert.Empty(cmd.AvailableComponents);
    }

    [AvaloniaFact]
    public void Selecting_a_component_sets_its_type()
    {
        var editor = TestFactory.Editor();
        var node = editor.CreateNodeAt(0, 0);
        var entity = editor.CreateEntityAt(0, 100);
        editor.AddComponentToEntity(entity, "condition");

        var choice = TestFactory.AddChoice(editor, node);
        var cmd = TestFactory.AddCommand(editor, choice);
        cmd.TargetEntity = entity;
        cmd.SelectedComponent = cmd.AvailableComponents[0];

        Assert.Equal("condition", cmd.TargetComponentType);
    }
}
