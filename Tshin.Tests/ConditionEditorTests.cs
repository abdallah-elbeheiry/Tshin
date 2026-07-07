using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using Tshin.Core.Models;
using Tshin.Core.Utils.Systems;
using Tshin.Services;
using Tshin.ViewModels;
using Xunit;

namespace Tshin.Tests;

/// <summary>
/// Condition-editor VMs (recursive add/remove, model round-trip), the player's
/// Close/Hide projection into <see cref="PlayerViewModel.CurrentChoices"/>, and a
/// full export→import persistence pass for a gated choice.
/// </summary>
public class ConditionEditorTests
{
    private static (EditorViewModel editor, NodeViewModel node, EntityViewModel entity)
        BuildWithEntity(double gold = 0)
    {
        var editor = TestFactory.Editor();
        var node = editor.CreateNodeAt(0, 0);
        var entity = editor.CreateEntityAt(0, 100);
        editor.AddComponentToEntity(entity, "number");
        var comp = (NumberComponentViewModel)entity.Components[0];
        comp.Name = "Gold";
        comp.MaxValue = 999;
        comp.Value = gold;
        return (editor, node, entity);
    }

    // ── Model round-trip ──────────────────────────────────────────────────

    [AvaloniaFact]
    public void Atomic_condition_round_trips_through_the_view_model()
    {
        var (editor, node, entity) = BuildWithEntity();
        var choice = TestFactory.AddChoice(editor, node);

        choice.Condition = new AtomicConditionNode
        {
            EntityId = entity.Id, ComponentId = "Gold", Operator = ">=", TargetValue = "50"
        };

        var rebuilt = Assert.IsType<AtomicConditionNode>(choice.Condition);
        Assert.Equal(entity.Id, rebuilt.EntityId);
        Assert.Equal("Gold", rebuilt.ComponentId);
        Assert.Equal(">=", rebuilt.Operator);   // symbol ≥ maps back to backend >=
        Assert.Equal("50", rebuilt.TargetValue);
    }

    [AvaloniaFact]
    public void Nested_and_or_group_round_trips_through_the_view_model()
    {
        var (editor, node, entity) = BuildWithEntity();
        editor.AddComponentToEntity(entity, "text");
        ((TextComponentViewModel)entity.Components[1]).Name = "Name";
        var choice = TestFactory.AddChoice(editor, node);

        choice.Condition = new LogicalGroupNode
        {
            IsAnd = false,
            Children = new List<IConditionComponentNode>
            {
                new AtomicConditionNode { EntityId = entity.Id, ComponentId = "Gold", Operator = ">", TargetValue = "10" },
                new LogicalGroupNode
                {
                    IsAnd = true,
                    Children = new List<IConditionComponentNode>
                    {
                        new AtomicConditionNode { EntityId = entity.Id, ComponentId = "Name", Operator = "==", TargetValue = "Kai" }
                    }
                }
            }
        };

        var root = Assert.IsType<LogicalGroupNode>(choice.Condition);
        Assert.False(root.IsAnd);
        Assert.Equal(2, root.Children.Count);

        var gold = Assert.IsType<AtomicConditionNode>(root.Children[0]);
        Assert.Equal(">", gold.Operator);
        Assert.Equal("10", gold.TargetValue);

        var inner = Assert.IsType<LogicalGroupNode>(root.Children[1]);
        Assert.True(inner.IsAnd);
        var name = Assert.IsType<AtomicConditionNode>(inner.Children[0]);
        Assert.Equal("Name", name.ComponentId);
        Assert.Equal("==", name.Operator);
        Assert.Equal("Kai", name.TargetValue);
    }

    // ── Editor commands ───────────────────────────────────────────────────

    [AvaloniaFact]
    public void Add_condition_seeds_an_and_group_with_one_atomic()
    {
        var (editor, node, _) = BuildWithEntity();
        var choice = TestFactory.AddChoice(editor, node);

        editor.AddConditionToChoiceCommand.Execute(choice);

        Assert.True(choice.HasCondition);
        var group = Assert.IsType<LogicalGroupViewModel>(choice.ConditionRoot);
        Assert.True(group.IsAnd);
        Assert.IsType<AtomicConditionViewModel>(Assert.Single(group.Children));
    }

    [AvaloniaFact]
    public void Removing_the_last_node_clears_the_whole_condition()
    {
        var (editor, node, _) = BuildWithEntity();
        var choice = TestFactory.AddChoice(editor, node);
        editor.AddConditionToChoiceCommand.Execute(choice);
        var atomic = ((LogicalGroupViewModel)choice.ConditionRoot!).Children[0];

        editor.RemoveConditionNodeCommand.Execute(atomic);

        Assert.False(choice.HasCondition);
        Assert.Null(choice.ConditionRoot);
    }

    [AvaloniaFact]
    public void Add_and_remove_a_nested_group_keeps_the_root()
    {
        var (editor, node, _) = BuildWithEntity();
        var choice = TestFactory.AddChoice(editor, node);
        editor.AddConditionToChoiceCommand.Execute(choice);
        var group = (LogicalGroupViewModel)choice.ConditionRoot!;

        editor.AddGroupToGroupCommand.Execute(group);
        Assert.Equal(2, group.Children.Count);                 // original atomic + new group
        var nested = Assert.IsType<LogicalGroupViewModel>(group.Children[1]);
        Assert.Single(nested.Children);                        // seeded with one atomic

        editor.RemoveConditionNodeCommand.Execute(nested);
        Assert.True(choice.HasCondition);                      // root still has the atomic
        Assert.Single(group.Children);
    }

    // ── Player Close/Hide projection ──────────────────────────────────────

    private static (EditorViewModel editor, NodeViewModel node) BuildGated(
        double gold, ConditionFalseBehavior behavior)
    {
        var (editor, node, entity) = BuildWithEntity(gold);
        var choice = TestFactory.AddChoice(editor, node);
        choice.Condition = new AtomicConditionNode
        {
            EntityId = entity.Id, ComponentId = "Gold", Operator = ">=", TargetValue = "50"
        };
        choice.ConditionFalseBehavior = behavior;
        return (editor, node);
    }

    [AvaloniaFact]
    public void Hide_behavior_omits_the_choice_from_current_choices()
    {
        var (editor, node) = BuildGated(gold: 10, ConditionFalseBehavior.Hide);
        var player = new PlayerViewModel(node, editor.Entities, () => { });
        Assert.Empty(player.CurrentChoices);
    }

    [AvaloniaFact]
    public void Close_behavior_keeps_the_choice_but_marks_it_not_openable()
    {
        var (editor, node) = BuildGated(gold: 10, ConditionFalseBehavior.Close);
        var player = new PlayerViewModel(node, editor.Entities, () => { });
        var pc = Assert.Single(player.CurrentChoices);
        Assert.False(pc.IsOpenable);
    }

    [AvaloniaFact]
    public void Satisfied_condition_is_openable()
    {
        var (editor, node) = BuildGated(gold: 100, ConditionFalseBehavior.Hide);
        var player = new PlayerViewModel(node, editor.Entities, () => { });
        var pc = Assert.Single(player.CurrentChoices);
        Assert.True(pc.IsOpenable);   // condition true → shown even under Hide
    }

    // ── Full persistence pass ─────────────────────────────────────────────

    [AvaloniaFact]
    public async Task Condition_and_false_behavior_survive_export_import()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tshin_cond_{Guid.NewGuid():N}.tshin");
        try
        {
            var editor = TestFactory.Editor("Cond");
            var n1 = editor.CreateNodeAt(0, 0);
            var n2 = editor.CreateNodeAt(300, 0);
            var entity = editor.CreateEntityAt(0, 300);
            entity.Name = "Hero";
            editor.AddComponentToEntity(entity, "number");
            var gold = (NumberComponentViewModel)entity.Components[0];
            gold.Name = "Gold";
            gold.MaxValue = 999;
            gold.Value = 100;

            var choice = TestFactory.AddChoice(editor, n1);
            choice.DisplayText = "Enter";
            editor.Connect(choice, n2);
            choice.Condition = new LogicalGroupNode
            {
                IsAnd = true,
                Children = new List<IConditionComponentNode>
                {
                    new AtomicConditionNode { EntityId = entity.Id, ComponentId = "Gold", Operator = ">=", TargetValue = "50" }
                }
            };
            choice.ConditionFalseBehavior = ConditionFalseBehavior.Hide;

            await editor.ExportCommand.ExecuteAsync(path);

            var service = new MockProjectService();
            var summary = await service.ImportProjectAsync(path);
            var story = await service.OpenProjectAsync(summary.Id);

            // Snapshot carried the false-behavior + a non-null condition.
            var storyChoice = story.Nodes.Single(n => n.Id == n1.Id).Choices.Single();
            Assert.Equal(ConditionFalseBehavior.Hide, storyChoice.ConditionFalseBehavior);
            Assert.NotNull(storyChoice.Condition);

            // Rebuild the editor and play it: Gold=100 satisfies >=50, so the gated
            // choice is present and openable — proving the tree evaluates after a round trip.
            var reopened = new EditorViewModel(story, "Cond", service);
            var rChoice = reopened.Nodes.Single(n => n.Id == n1.Id).Choices.Single();
            Assert.True(rChoice.HasCondition);
            Assert.Equal(ConditionFalseBehavior.Hide, rChoice.ConditionFalseBehavior);

            var player = new PlayerViewModel(reopened.Nodes.First(), reopened.Entities, () => { });
            var pc = Assert.Single(player.CurrentChoices);
            Assert.True(pc.IsOpenable);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
