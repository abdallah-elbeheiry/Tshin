using System.Linq;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Tshin.Tests;

/// <summary>
/// Snapshot undo/redo: one logical action is one entry, continuous runs coalesce, and a
/// restore rebuilds the graph while preserving viewport and selection.
/// </summary>
public class UndoRedoTests
{
    [AvaloniaFact]
    public void Adding_a_node_is_one_undo_step_and_redoable()
    {
        var editor = TestFactory.Editor();
        editor.CreateNodeAt(0, 0);
        Assert.Single(editor.Nodes);

        editor.UndoCommand.Execute(null);
        Assert.Empty(editor.Nodes);

        editor.RedoCommand.Execute(null);
        Assert.Single(editor.Nodes);
    }

    [AvaloniaFact]
    public void A_drag_of_many_deltas_undoes_in_one_step()
    {
        var editor = TestFactory.Editor();
        editor.CreateNodeAt(0, 0);

        var node = editor.Nodes.Single();
        node.X = 10;
        node.X = 25;
        node.Y = 40;               // coalesced "pos" run
        editor.FlushHistory();     // pointer release closes the drag

        editor.UndoCommand.Execute(null);
        var restored = editor.Nodes.Single();
        Assert.Equal(0, restored.X);
        Assert.Equal(0, restored.Y);
    }

    [AvaloniaFact]
    public void A_typing_run_undoes_in_one_step()
    {
        var editor = TestFactory.Editor();
        editor.CreateNodeAt(0, 0);

        var node = editor.Nodes.Single();
        node.DisplayText = "a";
        node.DisplayText = "ab";
        node.DisplayText = "abc";   // coalesced "text" run
        editor.FlushHistory();

        editor.UndoCommand.Execute(null);
        Assert.Equal("New node", editor.Nodes.Single().DisplayText);
    }

    [AvaloniaFact]
    public void Connecting_and_disconnecting_each_undo_in_one_step()
    {
        var editor = TestFactory.Editor();
        var n1 = editor.CreateNodeAt(0, 0);
        var n2 = editor.CreateNodeAt(400, 0);
        var choice = TestFactory.AddChoice(editor, n1);

        editor.Connect(choice, n2);
        Assert.Single(editor.Connections);

        editor.UndoCommand.Execute(null);
        Assert.Empty(editor.Connections);

        editor.RedoCommand.Execute(null);
        Assert.Single(editor.Connections);
    }

    [AvaloniaFact]
    public void Undo_preserves_viewport_and_selection()
    {
        var editor = TestFactory.Editor();
        var node = editor.CreateNodeAt(0, 0);
        editor.SetZoom(1.5);
        editor.OffsetX = 42;
        editor.OffsetY = 24;
        editor.SelectNode(node);

        node.X = 50;
        editor.FlushHistory();

        editor.UndoCommand.Execute(null);

        Assert.Equal(1.5, editor.Zoom);
        Assert.Equal(42, editor.OffsetX);
        Assert.Equal(24, editor.OffsetY);
        Assert.NotNull(editor.SelectedNode);
        Assert.Equal(node.Id, editor.SelectedNode!.Id);
    }

    [AvaloniaFact]
    public void Undo_is_unavailable_on_a_fresh_editor()
    {
        var editor = TestFactory.Editor();
        Assert.False(editor.CanUndo);
        Assert.False(editor.CanRedo);
    }
}
