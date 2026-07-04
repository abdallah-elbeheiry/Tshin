using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using Tshin.Models;
using Tshin.Services;
using Tshin.ViewModels;
using Xunit;

namespace Tshin.Tests;

/// <summary>
/// Export → import round trip through the real <see cref="Tshin.Core.Utils.Systems.FileWriter"/> /
/// <see cref="Tshin.Core.Utils.Systems.FileReader"/>. This is the data path behind the
/// macOS import bug; the parser/writer are culture-invariant, which these assertions pin down.
/// </summary>
public class FileRoundTripTests
{
    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), $"tshin_test_{Guid.NewGuid():N}.tshin");

    [AvaloniaFact]
    public async Task Nodes_entities_components_and_choices_survive_a_round_trip()
    {
        var path = TempFile();
        try
        {
            // --- Author a graph ---
            var editor = TestFactory.Editor("Round Trip");
            var n1 = editor.CreateNodeAt(10, 20);
            n1.DisplayText = "Intro";
            var n2 = editor.CreateNodeAt(300, 20);
            n2.DisplayText = "Cave";

            var entity = editor.CreateEntityAt(0, 300);
            entity.Name = "Hero";
            editor.AddComponentToEntity(entity, "number");
            var hp = (NumberComponentViewModel)entity.Components[0];
            hp.Name = "hp";
            hp.MinValue = 0;
            hp.MaxValue = 100;
            hp.Value = 7.5;             // decimal → guards invariant number formatting
            editor.AddComponentToEntity(entity, "text");
            var who = (TextComponentViewModel)entity.Components[1];
            who.Name = "who";
            who.Value = "Kai";
            editor.AddComponentToEntity(entity, "condition");
            var flag = (ConditionComponentViewModel)entity.Components[2];
            flag.Name = "flag";
            flag.Value = true;
            flag.Visible = false;       // guards component visibility

            var choice = TestFactory.AddChoice(editor, n1);
            choice.DisplayText = "Go";
            editor.Connect(choice, n2);
            var cmd = TestFactory.AddCommand(editor, choice);
            cmd.TargetEntity = entity;
            cmd.SelectedComponent = hp;
            cmd.SelectedFieldIndex = cmd.AvailableFields.IndexOf("Increase");
            cmd.NumberValue = 3;

            // --- Export to disk ---
            await editor.ExportCommand.ExecuteAsync(path);
            Assert.True(File.Exists(path));

            // --- Import into a clean service and reopen ---
            var service = new MockProjectService();
            var summary = await service.ImportProjectAsync(path);
            var story = await service.OpenProjectAsync(summary.Id);

            // Nodes
            Assert.Equal(2, story.Nodes.Count);
            var intro = story.Nodes.Single(n => n.Id == n1.Id);
            Assert.Equal("Intro", intro.DisplayText);

            // Choice + link
            var go = intro.Choices.Single();
            Assert.Equal("Go", go.DisplayText);
            Assert.Equal(n2.Id, go.TargetNodeId);

            // Command
            var numCmd = Assert.IsType<ModifyNumberCommandSnapshot>(go.Commands.Single());
            Assert.Equal("hp", numCmd.TargetComponentName);
            Assert.Equal(3, numCmd.Value);
            Assert.Equal("Increase", numCmd.Field);

            // Entity + components (found by name, order not guaranteed)
            var e = story.Entities.Single();
            Assert.Equal("Hero", e.Name);
            var num = Assert.IsType<NumberComponentSnapshot>(e.Components.Single(c => c.Name == "hp"));
            Assert.Equal(7.5, num.Value);
            Assert.Equal(100, num.MaxValue);
            var text = Assert.IsType<TextComponentSnapshot>(e.Components.Single(c => c.Name == "who"));
            Assert.Equal("Kai", text.Value);
            var cond = Assert.IsType<ConditionComponentSnapshot>(e.Components.Single(c => c.Name == "flag"));
            Assert.True(cond.Value);
            Assert.False(cond.Visible);

            // The command's target entity id resolves to the imported entity.
            Assert.Equal(e.Id, numCmd.TargetEntityId);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [AvaloniaFact]
    public async Task Exported_file_uses_invariant_number_formatting()
    {
        var path = TempFile();
        try
        {
            var editor = TestFactory.Editor();
            var entity = editor.CreateEntityAt(0, 0);
            editor.AddComponentToEntity(entity, "number");
            var n = (NumberComponentViewModel)entity.Components[0];
            n.Value = 3.5;

            await editor.ExportCommand.ExecuteAsync(path);
            var contents = await File.ReadAllTextAsync(path);

            // A dot decimal separator regardless of the host machine's culture.
            Assert.Contains("3.5", contents);
            Assert.DoesNotContain("3,5", contents);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
