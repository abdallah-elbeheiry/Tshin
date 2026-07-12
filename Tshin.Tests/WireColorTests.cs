using Avalonia.Headless.XUnit;
using Tshin.ViewModels;
using Xunit;

namespace Tshin.Tests;

/// <summary>
/// Wire colours must be a deterministic function of the graph, not global construction
/// order. Two identically-built editors must produce identical per-connection colours.
/// </summary>
public class WireColorTests
{
    private static EditorViewModel BuildLinkedPair()
    {
        var editor = TestFactory.Editor("Wires");
        var n1 = editor.CreateNodeAt(0, 0);
        var n2 = editor.CreateNodeAt(400, 0);
        var choice = TestFactory.AddChoice(editor, n1);
        editor.Connect(choice, n2);
        return editor;
    }

    [AvaloniaFact]
    public void Two_identical_editors_produce_identical_wire_colors()
    {
        var a = BuildLinkedPair();
        var b = BuildLinkedPair();

        Assert.Equal(a.Connections.Count, b.Connections.Count);
        for (var i = 0; i < a.Connections.Count; i++)
            Assert.Same(a.Connections[i].Stroke, b.Connections[i].Stroke);
    }

    [AvaloniaFact]
    public void Distinct_connections_get_distinct_colors()
    {
        var editor = TestFactory.Editor("Wires");
        var n1 = editor.CreateNodeAt(0, 0);
        var n2 = editor.CreateNodeAt(400, 0);
        var c1 = TestFactory.AddChoice(editor, n1);
        var c2 = TestFactory.AddChoice(editor, n1);
        editor.Connect(c1, n2);
        editor.Connect(c2, n2);

        Assert.Equal(2, editor.Connections.Count);
        Assert.NotSame(editor.Connections[0].Stroke, editor.Connections[1].Stroke);
    }
}
