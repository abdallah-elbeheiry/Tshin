using Avalonia.Headless.XUnit;
using Tshin.ViewModels;
using Xunit;

namespace Tshin.Tests;

/// <summary>Bug 6: the component inspector's "Back to entity" must return to the owner.</summary>
public class EditorSelectionTests
{
    [AvaloniaFact]
    public void Selecting_a_component_anchors_on_its_owning_entity()
    {
        var editor = TestFactory.Editor();
        var entity = editor.CreateEntityAt(0, 0);
        editor.AddComponentToEntity(entity, "number");
        var comp = entity.Components[0];

        editor.SelectComponent(comp);

        Assert.Same(comp, editor.SelectedComponent);
        Assert.Same(entity, editor.SelectedEntity);
        Assert.Same(comp, editor.SelectedInspectorTarget);
    }

    [AvaloniaFact]
    public void Back_to_entity_returns_to_owner_even_after_a_choice_was_selected()
    {
        var editor = TestFactory.Editor();
        var node = editor.CreateNodeAt(0, 0);
        var entity = editor.CreateEntityAt(0, 100);
        editor.AddComponentToEntity(entity, "text");
        var comp = entity.Components[0];
        var choice = TestFactory.AddChoice(editor, node);

        // Simulate the buggy sequence: open a choice, then a component badge.
        editor.SelectChoice(choice);
        editor.SelectComponent(comp);

        editor.BackToEntityCommand.Execute(null);

        // Must land on the entity — NOT fall back to the previously open choice.
        Assert.Null(editor.SelectedComponent);
        Assert.Null(editor.SelectedChoice);
        Assert.Same(entity, editor.SelectedInspectorTarget);
    }

    [AvaloniaFact]
    public void Selecting_a_node_clears_component_and_entity()
    {
        var editor = TestFactory.Editor();
        var node = editor.CreateNodeAt(0, 0);
        var entity = editor.CreateEntityAt(0, 100);
        editor.AddComponentToEntity(entity, "number");
        editor.SelectComponent(entity.Components[0]);

        editor.SelectNode(node);

        Assert.Null(editor.SelectedComponent);
        Assert.Null(editor.SelectedEntity);
        Assert.Same(node, editor.SelectedInspectorTarget);
    }
}
