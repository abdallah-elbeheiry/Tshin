using System;
using System.IO;
using System.Threading.Tasks;
using Tshin.Core.Models;
using Tshin.Core.Utils.Commands;
using Tshin.Core.Utils.Factories;
using Tshin.Core.Utils.Managers;
using Tshin.Core.Utils.Systems;
using Xunit;

namespace Tshin.Tests;

/// <summary>
/// Tests for the Tshin DSL parser (<see cref="FileReader"/>), writer
/// (<see cref="FileWriter"/>), and the recursive condition evaluation
/// system (<see cref="AtomicConditionNode"/>, <see cref="LogicalGroupNode"/>).
/// </summary>
public class FileParsingTests
{
    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), $"tshin_test_{Guid.NewGuid():N}.tshin");

    #region Atomic Condition Node

    [Fact]
    public void AtomicConditionNode_number_greater_than_returns_true()
    {
        var em = new EntityManager();
        var entity = em.CreateEntity();
        em.SetComponent(entity, new NumberComponent { Name = "Gold", Value = 100 });

        var node = new AtomicConditionNode
        {
            EntityId = entity.Id.ToString(),
            ComponentId = "Gold",
            Operator = ">",
            TargetValue = "50"
        };

        Assert.True(node.Evaluate(em));
    }

    [Fact]
    public void AtomicConditionNode_number_less_than_or_equal_returns_false()
    {
        var em = new EntityManager();
        var entity = em.CreateEntity();
        em.SetComponent(entity, new NumberComponent { Name = "HP", Value = 10 });

        var node = new AtomicConditionNode
        {
            EntityId = entity.Id.ToString(),
            ComponentId = "HP",
            Operator = ">=",
            TargetValue = "20"
        };

        Assert.False(node.Evaluate(em));
    }

    [Fact]
    public void AtomicConditionNode_text_equality_returns_true()
    {
        var em = new EntityManager();
        var entity = em.CreateEntity();
        em.SetComponent(entity, new TextComponent { Name = "Name", Value = "Kai" });

        var node = new AtomicConditionNode
        {
            EntityId = entity.Id.ToString(),
            ComponentId = "Name",
            Operator = "==",
            TargetValue = "Kai"
        };

        Assert.True(node.Evaluate(em));
    }

    [Fact]
    public void AtomicConditionNode_boolean_equality_returns_true()
    {
        var em = new EntityManager();
        var entity = em.CreateEntity();
        em.SetComponent(entity, new ConditionComponent { Name = "HasKey", Value = true });

        var node = new AtomicConditionNode
        {
            EntityId = entity.Id.ToString(),
            ComponentId = "HasKey",
            Operator = "==",
            TargetValue = "true"
        };

        Assert.True(node.Evaluate(em));
    }

    [Fact]
    public void AtomicConditionNode_missing_entity_returns_false()
    {
        var em = new EntityManager();
        var node = new AtomicConditionNode
        {
            EntityId = Guid.NewGuid().ToString(),
            ComponentId = "Gold",
            Operator = ">",
            TargetValue = "50"
        };

        Assert.False(node.Evaluate(em));
    }

    [Fact]
    public void AtomicConditionNode_missing_component_returns_false()
    {
        var em = new EntityManager();
        var entity = em.CreateEntity();

        var node = new AtomicConditionNode
        {
            EntityId = entity.Id.ToString(),
            ComponentId = "NonExistent",
            Operator = "==",
            TargetValue = "true"
        };

        Assert.False(node.Evaluate(em));
    }

    [Fact]
    public void AtomicConditionNode_invalid_guid_returns_false()
    {
        var em = new EntityManager();
        var node = new AtomicConditionNode
        {
            EntityId = "not-a-guid",
            ComponentId = "Gold",
            Operator = ">",
            TargetValue = "50"
        };

        Assert.False(node.Evaluate(em));
    }

    #endregion

    #region Logical Group Node

    [Fact]
    public void LogicalGroupNode_and_all_true_returns_true()
    {
        var em = new EntityManager();
        var entity = em.CreateEntity();
        em.SetComponent(entity, new NumberComponent { Name = "Gold", Value = 100 });
        em.SetComponent(entity, new NumberComponent { Name = "Level", Value = 10 });

        var group = new LogicalGroupNode
        {
            IsAnd = true,
            Children =
            {
                new AtomicConditionNode { EntityId = entity.Id.ToString(), ComponentId = "Gold", Operator = ">", TargetValue = "50" },
                new AtomicConditionNode { EntityId = entity.Id.ToString(), ComponentId = "Level", Operator = ">=", TargetValue = "5" }
            }
        };

        Assert.True(group.Evaluate(em));
    }

    [Fact]
    public void LogicalGroupNode_and_one_false_returns_false()
    {
        var em = new EntityManager();
        var entity = em.CreateEntity();
        em.SetComponent(entity, new NumberComponent { Name = "Gold", Value = 30 });
        em.SetComponent(entity, new NumberComponent { Name = "Level", Value = 10 });

        var group = new LogicalGroupNode
        {
            IsAnd = true,
            Children =
            {
                new AtomicConditionNode { EntityId = entity.Id.ToString(), ComponentId = "Gold", Operator = ">", TargetValue = "50" },
                new AtomicConditionNode { EntityId = entity.Id.ToString(), ComponentId = "Level", Operator = ">=", TargetValue = "5" }
            }
        };

        Assert.False(group.Evaluate(em));
    }

    [Fact]
    public void LogicalGroupNode_or_any_true_returns_true()
    {
        var em = new EntityManager();
        var entity = em.CreateEntity();
        em.SetComponent(entity, new NumberComponent { Name = "Gold", Value = 100 });
        em.SetComponent(entity, new NumberComponent { Name = "Level", Value = 2 });

        var group = new LogicalGroupNode
        {
            IsAnd = false,
            Children =
            {
                new AtomicConditionNode { EntityId = entity.Id.ToString(), ComponentId = "Gold", Operator = ">", TargetValue = "50" },
                new AtomicConditionNode { EntityId = entity.Id.ToString(), ComponentId = "Level", Operator = ">=", TargetValue = "10" }
            }
        };

        Assert.True(group.Evaluate(em));
    }

    [Fact]
    public void LogicalGroupNode_or_all_false_returns_false()
    {
        var em = new EntityManager();
        var entity = em.CreateEntity();
        em.SetComponent(entity, new NumberComponent { Name = "Gold", Value = 10 });
        em.SetComponent(entity, new NumberComponent { Name = "Level", Value = 2 });

        var group = new LogicalGroupNode
        {
            IsAnd = false,
            Children =
            {
                new AtomicConditionNode { EntityId = entity.Id.ToString(), ComponentId = "Gold", Operator = ">", TargetValue = "50" },
                new AtomicConditionNode { EntityId = entity.Id.ToString(), ComponentId = "Level", Operator = ">=", TargetValue = "10" }
            }
        };

        Assert.False(group.Evaluate(em));
    }

    [Fact]
    public void LogicalGroupNode_nested_and_inside_or_evaluates_correctly()
    {
        var em = new EntityManager();
        var entity = em.CreateEntity();
        em.SetComponent(entity, new NumberComponent { Name = "Gold", Value = 10 });
        em.SetComponent(entity, new NumberComponent { Name = "Level", Value = 2 });
        em.SetComponent(entity, new ConditionComponent { Name = "HasKey", Value = true });
        em.SetComponent(entity, new ConditionComponent { Name = "DoorLocked", Value = true });

        // or(
        //   "Gold" > 50
        //   "Level" >= 10
        //   and(
        //     "HasKey" == true
        //     "DoorLocked" == true
        //   )
        // )
        var group = new LogicalGroupNode
        {
            IsAnd = false,
            Children =
            {
                new AtomicConditionNode { EntityId = entity.Id.ToString(), ComponentId = "Gold", Operator = ">", TargetValue = "50" },
                new AtomicConditionNode { EntityId = entity.Id.ToString(), ComponentId = "Level", Operator = ">=", TargetValue = "10" },
                new LogicalGroupNode
                {
                    IsAnd = true,
                    Children =
                    {
                        new AtomicConditionNode { EntityId = entity.Id.ToString(), ComponentId = "HasKey", Operator = "==", TargetValue = "true" },
                        new AtomicConditionNode { EntityId = entity.Id.ToString(), ComponentId = "DoorLocked", Operator = "==", TargetValue = "true" }
                    }
                }
            }
        };

        Assert.True(group.Evaluate(em));
    }

    [Fact]
    public void LogicalGroupNode_empty_and_returns_true()
    {
        var group = new LogicalGroupNode { IsAnd = true };
        Assert.True(group.Evaluate(new EntityManager()));
    }

    [Fact]
    public void LogicalGroupNode_empty_or_returns_false()
    {
        var group = new LogicalGroupNode { IsAnd = false };
        Assert.False(group.Evaluate(new EntityManager()));
    }

    #endregion

    #region File Round-Trip with New Syntax

    [Fact]
    public async Task Mutations_with_new_syntax_round_trip_correctly()
    {
        var path = TempFile();
        try
        {
            // Write a .tshin file using the new mutation syntax (no commas, colons retained)
            var tshinContent =
                "[Entity: \"e1\"]\n" +
                "  position: 0,0\n" +
                "  name: \"TestEntity\"\n" +
                "  visible: true\n" +
                "  number: \"Score\"\n" +
                "  {\n" +
                "    value: 50\n" +
                "    min: 0\n" +
                "    max: 100\n" +
                "    visible: true\n" +
                "  }\n" +
                "\n" +
                "[StoryNode: \"start\"]\n" +
                "  text: \"Begin\"\n" +
                "  position: 10,20\n" +
                "  choice: \"Go north\"->null\n" +
                "  {\n" +
                "    reduce: \"e1\" \"Score\" 10\n" +
                "    increase: \"e1\" \"Score\" 5\n" +
                "    set: \"e1\" \"Score\" 99\n" +
                "  }\n";

            await File.WriteAllTextAsync(path, tshinContent, TestContext.Current.CancellationToken);

            var em = new EntityManager();
            var nm = new NodeManager();
            await FileReader.LoadFileAsync(path, em, nm);

            var nodes = nm.GetNodes();
            Assert.Single(nodes);

            var branching = Assert.IsAssignableFrom<IBranchingNode>(nodes[0]);
            Assert.Single(branching.Choices);

            var choice = branching.Choices[0];
            Assert.Equal(3, choice.Commands.Count);

            var reduceCmd = Assert.IsType<ModifyNumberCommand>(choice.Commands[0]);
            var increaseCmd = Assert.IsType<ModifyNumberCommand>(choice.Commands[1]);
            var setCmd = Assert.IsType<ModifyNumberCommand>(choice.Commands[2]);

            Assert.Equal("Score", reduceCmd.TargetComponentName);
            Assert.Equal(10, reduceCmd.Value);
            Assert.Equal(CommandField.Reduce, reduceCmd.Field);

            Assert.Equal("Score", increaseCmd.TargetComponentName);
            Assert.Equal(5, increaseCmd.Value);
            Assert.Equal(CommandField.Increase, increaseCmd.Field);

            Assert.Equal("Score", setCmd.TargetComponentName);
            Assert.Equal(99, setCmd.Value);
            Assert.Equal(CommandField.Set, setCmd.Field);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Requirement_parses_and_evaluates_in_round_trip()
    {
        var path = TempFile();
        try
        {
            var entityId = Guid.NewGuid().ToString();

            var tshinContent =
                "[Entity: \"" + entityId + "\"]\n" +
                "  position: 0,0\n" +
                "  name: \"Player\"\n" +
                "  visible: true\n" +
                "  number: \"Gold\"\n" +
                "  {\n" +
                "    value: 100\n" +
                "    min: 0\n" +
                "    max: 9999\n" +
                "    visible: true\n" +
                "  }\n" +
                "  number: \"Level\"\n" +
                "  {\n" +
                "    value: 5\n" +
                "    min: 1\n" +
                "    max: 99\n" +
                "    visible: true\n" +
                "  }\n" +
                "\n" +
                "[StoryNode: \"start\"]\n" +
                "  text: \"Intro\"\n" +
                "  position: 10,20\n" +
                "  choice: \"Fight enemy\"->null\n" +
                "  {\n" +
                "    require: or(\n" +
                "      \"" + entityId + "\" \"Gold\" >= 50\n" +
                "      \"" + entityId + "\" \"Level\" > 5\n" +
                "    )\n" +
                "    reduce: \"" + entityId + "\" \"Gold\" 10\n" +
                "  }\n";

            await File.WriteAllTextAsync(path, tshinContent, TestContext.Current.CancellationToken);

            var em = new EntityManager();
            var nm = new NodeManager();
            await FileReader.LoadFileAsync(path, em, nm);

            var nodes = nm.GetNodes();
            var branching = Assert.IsAssignableFrom<IBranchingNode>(nodes[0]);
            var choice = branching.Choices[0];

            // Verify condition was parsed
            Assert.NotNull(choice.Condition);
            var group = Assert.IsType<LogicalGroupNode>(choice.Condition);
            Assert.False(group.IsAnd);
            Assert.Equal(2, group.Children.Count);

            // Evaluate against the loaded state (Gold=100, Level=5)
            // or(Gold>=50, Level>5) → true (Gold>=50 is true)
            Assert.True(choice.Condition.Evaluate(em));

            // Verify commands also parsed
            Assert.Single(choice.Commands);
            var cmd = Assert.IsType<ModifyNumberCommand>(choice.Commands[0]);
            Assert.Equal("Gold", cmd.TargetComponentName);
            Assert.Equal(10, cmd.Value);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Nested_and_inside_requirement_parses_and_evaluates()
    {
        var path = TempFile();
        try
        {
            var entityId = Guid.NewGuid().ToString();

            var tshinContent =
                "[Entity: \"" + entityId + "\"]\n" +
                "  position: 0,0\n" +
                "  name: \"Player\"\n" +
                "  visible: true\n" +
                "  boolean: \"HasKey\"\n" +
                "  {\n" +
                "    value: true\n" +
                "    visible: true\n" +
                "  }\n" +
                "  boolean: \"DoorLocked\"\n" +
                "  {\n" +
                "    value: true\n" +
                "    visible: true\n" +
                "  }\n" +
                "\n" +
                "[StoryNode: \"start\"]\n" +
                "  text: \"Door room\"\n" +
                "  position: 10,20\n" +
                "  choice: \"Open door\"->null\n" +
                "  {\n" +
                "    require: and(\n" +
                "      \"" + entityId + "\" \"HasKey\" == true\n" +
                "      \"" + entityId + "\" \"DoorLocked\" == true\n" +
                "    )\n" +
                "    set: \"" + entityId + "\" \"DoorLocked\" false\n" +
                "  }\n";

            await File.WriteAllTextAsync(path, tshinContent, TestContext.Current.CancellationToken);

            var em = new EntityManager();
            var nm = new NodeManager();
            await FileReader.LoadFileAsync(path, em, nm);

            var nodes = nm.GetNodes();
            var branching = Assert.IsAssignableFrom<IBranchingNode>(nodes[0]);
            var choice = branching.Choices[0];

            Assert.NotNull(choice.Condition);
            var group = Assert.IsType<LogicalGroupNode>(choice.Condition);
            Assert.True(group.IsAnd);
            Assert.Equal(2, group.Children.Count);

            // HasKey=true AND DoorLocked=true → true
            Assert.True(choice.Condition.Evaluate(em));

            // Toggle HasKey to false → should now be false
            em.GetComponent<ConditionComponent>(em.FindEntity(Guid.Parse(entityId))!, "HasKey")!.Value = false;
            Assert.False(choice.Condition.Evaluate(em));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Requirement_without_gold_condition_choice_is_always_available()
    {
        var path = TempFile();
        try
        {
            var tshinContent =
                "[StoryNode: \"start\"]\n" +
                "  text: \"Start\"\n" +
                "  position: 0,0\n" +
                "  choice: \"Always available\"->null\n" +
                "  {\n" +
                "    set: \"nonexistent\" \"X\" 1\n" +
                "  }\n";

            await File.WriteAllTextAsync(path, tshinContent, TestContext.Current.CancellationToken);

            var em = new EntityManager();
            var nm = new NodeManager();
            await FileReader.LoadFileAsync(path, em, nm);

            var nodes = nm.GetNodes();
            var branching = Assert.IsAssignableFrom<IBranchingNode>(nodes[0]);
            var choice = branching.Choices[0];

            // No condition → always available
            Assert.Null(choice.Condition);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Multi_line_unbalanced_require_aggregates_correctly()
    {
        var path = TempFile();
        try
        {
            var entityId = Guid.NewGuid().ToString();

            // The require: starts with only "and(" on the first line
            // and the closing ")" is on a later line
            var tshinContent =
                "[Entity: \"" + entityId + "\"]\n" +
                "  position: 0,0\n" +
                "  name: \"P\"\n" +
                "  visible: true\n" +
                "  boolean: \"Flag\"\n" +
                "  {\n" +
                "    value: true\n" +
                "    visible: true\n" +
                "  }\n" +
                "\n" +
                "[StoryNode: \"start\"]\n" +
                "  text: \"Test\"\n" +
                "  position: 0,0\n" +
                "  choice: \"Go\"->null\n" +
                "  {\n" +
                "    require: and(\n" +
                "      \"" + entityId + "\" \"Flag\" == true\n" +
                "    )\n" +
                "  }\n";

            await File.WriteAllTextAsync(path, tshinContent, TestContext.Current.CancellationToken);

            var em = new EntityManager();
            var nm = new NodeManager();
            await FileReader.LoadFileAsync(path, em, nm);

            var nodes = nm.GetNodes();
            var branching = Assert.IsAssignableFrom<IBranchingNode>(nodes[0]);
            var choice = branching.Choices[0];

            Assert.NotNull(choice.Condition);
            Assert.True(choice.Condition.Evaluate(em));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task FileWriter_produces_colon_verb_format_no_commas()
    {
        var path = TempFile();
        try
        {
            var em = new EntityManager();
            var entity = em.CreateEntity();
            em.SetComponent(entity, new NumberComponent { Name = "HP", Value = 100 });

            var nm = new NodeManager();
            var node = NodeFactory.CreateNode(NodeType.StoryNode, "n1");
            node.DisplayText = "Test";
            nm.AppendNode(node);

            if (nm.TryGetNode("n1", out var n) && n is IBranchingNode bn)
            {
                var choice = new Choice("Go");
                choice.Commands.Add(new ModifyNumberCommand
                {
                    Entity = entity,
                    TargetComponentName = "HP",
                    Value = 10,
                    Field = CommandField.Reduce
                });
                bn.Choices.Add(choice);
            }

            await FileWriter.SaveFileAsync(path, em, nm);

            var content = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
            Assert.Contains("reduce:", content);
            // Commas between arguments should NOT appear
            Assert.DoesNotContain("\", \"", content);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Condition_writes_and_reads_through_full_round_trip()
    {
        var path = TempFile();
        try
        {
            var em = new EntityManager();
            var entity = em.CreateEntity();
            em.SetComponent(entity, new NumberComponent { Name = "Gold", Value = 100 });

            var nm = new NodeManager();
            var node = NodeFactory.CreateNode(NodeType.StoryNode, "n1");
            node.DisplayText = "Test";
            nm.AppendNode(node);

            if (nm.TryGetNode("n1", out var n) && n is IBranchingNode bn)
            {
                var choice = new Choice("Rich only")
                {
                    Condition = new LogicalGroupNode
                    {
                        IsAnd = false,
                        Children =
                        {
                            new AtomicConditionNode
                            {
                                EntityId = entity.Id.ToString(),
                                ComponentId = "Gold",
                                Operator = ">=",
                                TargetValue = "50"
                            }
                        }
                    }
                };
                bn.Choices.Add(choice);
            }

            // Write to disk
            await FileWriter.SaveFileAsync(path, em, nm);

            // Read back into fresh managers
            var em2 = new EntityManager();
            var nm2 = new NodeManager();
            await FileReader.LoadFileAsync(path, em2, nm2);

            var nodes = nm2.GetNodes();
            var branching = Assert.IsAssignableFrom<IBranchingNode>(nodes[0]);
            var parsedChoice = branching.Choices[0];

            Assert.NotNull(parsedChoice.Condition);
            // The entity in the file has a different ID because we used CreateEntity()
            // which generates a random GUID. The condition references the original entity ID.
            // When the file writer serialized it, it wrote the original entity GUID.
            // When the file reader parsed it, it created a new entity with that GUID.
            // So the condition's entity ID should now match the new entity.
            Assert.True(parsedChoice.Condition.Evaluate(em2));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    #endregion
}
