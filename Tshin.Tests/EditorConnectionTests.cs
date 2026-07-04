using System.Linq;
using Avalonia.Headless.XUnit;
using Tshin.ViewModels;
using Xunit;

namespace Tshin.Tests;

/// <summary>Bug 7: choices can be linked and, crucially, unlinked again.</summary>
public class EditorConnectionTests
{
    [AvaloniaFact]
    public void Connect_sets_target_and_adds_a_wire()
    {
        var editor = TestFactory.Editor();
        var n1 = editor.CreateNodeAt(0, 0);
        var n2 = editor.CreateNodeAt(300, 0);
        var choice = TestFactory.AddChoice(editor, n1);

        editor.Connect(choice, n2);

        Assert.Same(n2, choice.Target);
        Assert.Single(editor.Connections);
    }

    [AvaloniaFact]
    public void Disconnect_clears_target_and_removes_the_wire()
    {
        var editor = TestFactory.Editor();
        var n1 = editor.CreateNodeAt(0, 0);
        var n2 = editor.CreateNodeAt(300, 0);
        var choice = TestFactory.AddChoice(editor, n1);
        editor.Connect(choice, n2);

        editor.Disconnect(choice);

        Assert.Null(choice.Target);
        Assert.Empty(editor.Connections);
    }

    [AvaloniaFact]
    public void Disconnect_on_an_unlinked_choice_is_a_noop()
    {
        var editor = TestFactory.Editor();
        var n1 = editor.CreateNodeAt(0, 0);
        var choice = TestFactory.AddChoice(editor, n1);

        editor.Disconnect(choice); // must not throw

        Assert.Null(choice.Target);
        Assert.Empty(editor.Connections);
    }

    [AvaloniaFact]
    public void Rebuild_produces_one_connection_per_linked_choice()
    {
        var editor = TestFactory.Editor();
        var n1 = editor.CreateNodeAt(0, 0);
        var n2 = editor.CreateNodeAt(300, 0);
        var c1 = TestFactory.AddChoice(editor, n1);
        var c2 = TestFactory.AddChoice(editor, n1);
        editor.Connect(c1, n2);
        editor.Connect(c2, n2);

        Assert.Equal(2, editor.Connections.Count);

        // A connection can compute its bezier geometry (needs the Avalonia platform).
        Assert.False(string.IsNullOrEmpty(editor.Connections.First().PathData));
    }
}
