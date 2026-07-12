using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Tshin.Services;
using Tshin.ViewModels;
using Tshin.Views;
using Xunit;

namespace Tshin.Tests;

/// <summary>
/// Renders the real windows on the headless platform. These would fail if a XAML
/// binding, converter, static resource, or data template were broken — the class of
/// regression that motivated this suite.
/// </summary>
public class UiRenderingTests
{
    private static void Pump() => Dispatcher.UIThread.RunJobs();

    [AvaloniaFact]
    public void MainWindow_renders_with_the_default_view_model()
    {
        var window = new MainWindow { DataContext = new MainWindowViewModel(new MockProjectService()) };
        window.Show();
        Pump();
        Assert.True(window.IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void EditorView_renders_and_the_epic_title_is_an_editable_textbox()
    {
        var editor = TestFactory.Editor("My Epic");
        var window = new Window { Content = new EditorView { DataContext = editor } };
        window.Show();
        Pump();

        // With no nodes/selection, the toolbar's project-title box is the only TextBox.
        var titleBoxes = window.GetVisualDescendants()
            .OfType<TextBox>()
            .Where(t => t.Text == "My Epic")
            .ToList();

        Assert.Single(titleBoxes);
        Assert.False(titleBoxes[0].IsReadOnly); // editable — the rename fix

        window.Close();
    }

    [AvaloniaFact]
    public void PlayerWindow_renders_choices_and_the_entities_panel()
    {
        // Graph: a start node with one choice, plus a visible entity with a component.
        var editor = TestFactory.Editor();
        var start = editor.CreateNodeAt(0, 0);
        start.DisplayText = "Once upon a time";
        var next = editor.CreateNodeAt(300, 0);
        var choice = TestFactory.AddChoice(editor, start);
        choice.DisplayText = "Continue";
        editor.Connect(choice, next);

        var entity = editor.CreateEntityAt(0, 200);
        entity.Name = "Hero";
        editor.AddComponentToEntity(entity, "number");
        ((NumberComponentViewModel)entity.Components[0]).Name = "hp";

        var player = new PlayerViewModel(start, editor.Entities);
        var window = new PlayerWindow { DataContext = player };
        window.Show();
        Pump();

        var texts = window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Contains("Once upon a time", texts);
        Assert.Contains("Entities", texts); // the collapsible panel header
        Assert.Contains("Hero", texts);     // entity listed in the panel

        // The choice button is present and clickable.
        var choiceButton = window.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(b => (b.Content as string) == "Continue");
        Assert.NotNull(choiceButton);

        window.Close();
    }

    [AvaloniaFact]
    public void PlayerWindow_hides_entities_panel_when_none_are_visible()
    {
        var editor = TestFactory.Editor();
        var start = editor.CreateNodeAt(0, 0);
        var player = new PlayerViewModel(start, editor.Entities);
        var window = new PlayerWindow { DataContext = player };
        window.Show();
        Pump();

        var texts = window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.DoesNotContain("Entities", texts);

        window.Close();
    }
}
