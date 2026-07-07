using System;
using System.Threading.Tasks;
using Tshin.Core.Models;
using Tshin.Core.Utils.Systems;
using Avalonia.Headless.XUnit;
using Tshin.Models;
using Tshin.Services;
using Tshin.ViewModels;
using Xunit;

namespace Tshin.Tests;

/// <summary>
/// Optional per-command conditions: the editor add/remove commands, node removal via the
/// shared recursive tree, and an in-memory save→open round trip through the snapshot layer.
/// </summary>
public class CommandConditionTests
{
    private static (EditorViewModel editor, NodeViewModel node, EntityViewModel entity, CommandViewModel cmd)
        BuildWithCommand()
    {
        var editor = TestFactory.Editor();
        var node = editor.CreateNodeAt(0, 0);
        var entity = editor.CreateEntityAt(0, 100);
        editor.AddComponentToEntity(entity, "number");
        ((NumberComponentViewModel)entity.Components[0]).Name = "Gold";

        var choice = TestFactory.AddChoice(editor, node);
        var cmd = TestFactory.AddCommand(editor, choice);
        cmd.TargetEntity = entity;
        cmd.SelectedComponent = cmd.AvailableComponents[0];
        return (editor, node, entity, cmd);
    }

    [AvaloniaFact]
    public void Command_starts_unconditional()
    {
        var (_, _, _, cmd) = BuildWithCommand();
        Assert.False(cmd.HasCondition);
        Assert.Null(cmd.Condition);
    }

    [AvaloniaFact]
    public void Add_condition_seeds_an_and_group_with_one_atomic()
    {
        var (editor, _, _, cmd) = BuildWithCommand();

        editor.AddConditionToCommandCommand.Execute(cmd);

        Assert.True(cmd.HasCondition);
        var group = Assert.IsType<LogicalGroupViewModel>(cmd.ConditionRoot);
        Assert.True(group.IsAnd);
        Assert.IsType<AtomicConditionViewModel>(Assert.Single(group.Children));
    }

    [AvaloniaFact]
    public void Remove_condition_clears_it()
    {
        var (editor, _, _, cmd) = BuildWithCommand();
        editor.AddConditionToCommandCommand.Execute(cmd);

        editor.RemoveConditionFromCommandCommand.Execute(cmd);

        Assert.False(cmd.HasCondition);
        Assert.Null(cmd.ConditionRoot);
    }

    [AvaloniaFact]
    public void Removing_the_last_node_clears_the_command_condition()
    {
        var (editor, _, _, cmd) = BuildWithCommand();
        editor.AddConditionToCommandCommand.Execute(cmd);
        var atomic = ((LogicalGroupViewModel)cmd.ConditionRoot!).Children[0];

        editor.RemoveConditionNodeCommand.Execute(atomic);

        Assert.False(cmd.HasCondition);
        Assert.Null(cmd.ConditionRoot);
    }

    [AvaloniaFact]
    public void Removing_a_nested_group_keeps_the_command_condition_root()
    {
        var (editor, _, _, cmd) = BuildWithCommand();
        editor.AddConditionToCommandCommand.Execute(cmd);
        var group = (LogicalGroupViewModel)cmd.ConditionRoot!;

        editor.AddGroupToGroupCommand.Execute(group);
        var nested = Assert.IsType<LogicalGroupViewModel>(group.Children[1]);

        editor.RemoveConditionNodeCommand.Execute(nested);

        Assert.True(cmd.HasCondition);   // root atomic remains
        Assert.Single(group.Children);
    }

    [AvaloniaFact]
    public async Task Command_condition_survives_in_memory_save_and_open()
    {
        // A gated command persisted through the mock service's in-memory snapshot store,
        // exercising both the clone and the snapshot→VM restore paths.
        var service = new MockProjectService();
        var summary = await service.CreateProjectAsync("Cmd");
        var entityId = Guid.NewGuid().ToString("D");

        var entitySnap = new EntitySnapshot { Id = entityId, Name = "Hero" };
        entitySnap.Components.Add(new NumberComponentSnapshot("Gold", 100, 0, 999));

        var cmdSnap = new ModifyNumberCommandSnapshot(entityId, "Gold", "Set", 5)
        {
            Condition = new AtomicConditionNode
            {
                EntityId = entityId, ComponentId = "Gold", Operator = ">=", TargetValue = "50"
            }
        };
        var choiceSnap = new ChoiceSnapshot { DisplayText = "Act" };
        choiceSnap.Commands.Add(cmdSnap);
        var nodeSnap = new NodeSnapshot { Id = "n1" };
        nodeSnap.Choices.Add(choiceSnap);

        var snapshot = new StorySnapshot { ProjectId = summary.Id };
        snapshot.Entities.Add(entitySnap);
        snapshot.Nodes.Add(nodeSnap);

        await service.SaveProjectAsync(snapshot);          // clones into the in-memory store
        var reopened = await service.OpenProjectAsync(summary.Id);
        var editor = new EditorViewModel(reopened, "Cmd", service);

        var rCmd = editor.Nodes[0].Choices[0].Commands[0];
        Assert.True(rCmd.HasCondition);
        var atomic = Assert.IsType<AtomicConditionNode>(rCmd.Condition);
        Assert.Equal("Gold", atomic.ComponentId);
        Assert.Equal(">=", atomic.Operator);
        Assert.Equal("50", atomic.TargetValue);
    }
}
