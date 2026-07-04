using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Headless.XUnit;
using Tshin.ViewModels;
using Xunit;

namespace Tshin.Tests;

/// <summary>Run-mode behaviour: command execution, targetless choices, entities panel.</summary>
public class PlayerViewModelTests
{
    /// <summary>Builds node1 --choice--> node2, where the choice increases entity "hp" by 5.</summary>
    private static (EditorViewModel editor, NodeViewModel n1, NodeViewModel n2,
                    ChoiceViewModel choice, NumberComponentViewModel comp)
        BuildGraph(bool linkChoice = true)
    {
        var editor = TestFactory.Editor();
        var n1 = editor.CreateNodeAt(0, 0);
        var n2 = editor.CreateNodeAt(300, 0);
        var entity = editor.CreateEntityAt(0, 300);
        editor.AddComponentToEntity(entity, "number");
        var comp = (NumberComponentViewModel)entity.Components[0];
        comp.MaxValue = 100;
        comp.Value = 10;

        var choice = TestFactory.AddChoice(editor, n1);
        if (linkChoice) editor.Connect(choice, n2);

        var cmd = TestFactory.AddCommand(editor, choice);
        cmd.TargetEntity = entity;
        cmd.SelectedComponent = comp;
        cmd.SelectedFieldIndex = cmd.AvailableFields.IndexOf("Increase");
        cmd.NumberValue = 5;

        return (editor, n1, n2, choice, comp);
    }

    [AvaloniaFact]
    public void Choosing_a_linked_choice_runs_commands_then_navigates()
    {
        var (editor, n1, n2, choice, editorComp) = BuildGraph();
        var player = new PlayerViewModel(n1, editor.Entities, () => { });

        player.ChooseCommand.Execute(choice);

        // Navigated to the linked node…
        Assert.Same(n2, player.CurrentNode);
        // …and the command ran against the play clone (10 + 5 = 15).
        var playComp = (NumberComponentViewModel)player.PlayEntities[0].Components[0];
        Assert.Equal(15, playComp.Value);
        // Author data untouched.
        Assert.Equal(10, editorComp.Value);
    }

    [AvaloniaFact]
    public void Targetless_choice_runs_commands_but_stays_on_the_same_node()
    {
        var (editor, n1, _, choice, _) = BuildGraph(linkChoice: false);
        var player = new PlayerViewModel(n1, editor.Entities, () => { });

        player.ChooseCommand.Execute(choice);

        Assert.Same(n1, player.CurrentNode); // stayed put
        var playComp = (NumberComponentViewModel)player.PlayEntities[0].Components[0];
        Assert.Equal(15, playComp.Value); // command still ran
    }

    [AvaloniaFact]
    public void Increase_is_clamped_to_max()
    {
        var (editor, n1, _, choice, _) = BuildGraph(linkChoice: false);
        var cmd = choice.Commands[0];
        cmd.NumberValue = 1000; // 10 + 1000, clamped to max 100

        var player = new PlayerViewModel(n1, editor.Entities, () => { });
        player.ChooseCommand.Execute(choice);

        var playComp = (NumberComponentViewModel)player.PlayEntities[0].Components[0];
        Assert.Equal(100, playComp.Value);
    }

    [AvaloniaFact]
    public void Command_mutation_updates_display_value()
    {
        var (editor, n1, _, choice, _) = BuildGraph(linkChoice: false);
        var player = new PlayerViewModel(n1, editor.Entities, () => { });

        player.ChooseCommand.Execute(choice);

        var playComp = (NumberComponentViewModel)player.PlayEntities[0].Components[0];
        Assert.Equal("15", playComp.DisplayValue);
    }

    [AvaloniaFact]
    public void Visible_play_entities_filters_hidden_ones()
    {
        var editor = TestFactory.Editor();
        var start = editor.CreateNodeAt(0, 0);
        var shown = editor.CreateEntityAt(0, 100);
        var hidden = editor.CreateEntityAt(0, 200);
        hidden.Visible = false;

        var player = new PlayerViewModel(start, editor.Entities, () => { });

        Assert.True(player.HasVisibleEntities);
        Assert.Single(player.VisiblePlayEntities);
        Assert.Equal(shown.Name, player.VisiblePlayEntities.First().Name);
    }

    [AvaloniaFact]
    public void No_visible_entities_reports_false()
    {
        var editor = TestFactory.Editor();
        var start = editor.CreateNodeAt(0, 0);
        var e = editor.CreateEntityAt(0, 100);
        e.Visible = false;

        var player = new PlayerViewModel(start, editor.Entities, () => { });

        Assert.False(player.HasVisibleEntities);
        Assert.Empty(player.VisiblePlayEntities);
    }

    [AvaloniaFact]
    public void Toggle_entities_panel_flips_state_and_glyph()
    {
        var editor = TestFactory.Editor();
        var start = editor.CreateNodeAt(0, 0);
        var player = new PlayerViewModel(start, editor.Entities, () => { });

        Assert.True(player.IsEntitiesPanelExpanded);
        var open = player.EntitiesToggleGlyph;

        player.ToggleEntitiesPanelCommand.Execute(null);

        Assert.False(player.IsEntitiesPanelExpanded);
        Assert.NotEqual(open, player.EntitiesToggleGlyph);
    }

    [AvaloniaFact]
    public void Restart_returns_to_the_start_node()
    {
        var (editor, n1, n2, choice, _) = BuildGraph();
        var player = new PlayerViewModel(n1, editor.Entities, () => { });
        player.ChooseCommand.Execute(choice);
        Assert.Same(n2, player.CurrentNode);

        player.RestartCommand.Execute(null);

        Assert.Same(n1, player.CurrentNode);
    }

    [AvaloniaFact]
    public void Is_end_true_when_node_has_no_choices()
    {
        var editor = TestFactory.Editor();
        var lone = editor.CreateNodeAt(0, 0);
        var player = new PlayerViewModel(lone, editor.Entities, () => { });
        Assert.True(player.IsEnd);
    }
}
