using Tshin.ViewModels;
using Xunit;

namespace Tshin.Tests;

/// <summary>
/// Locks the single source of truth for card geometry: <see cref="NodeLayout.NodeHeight"/>
/// must equal the old inline formula that the canvas hit-test and fit-to-view used.
/// </summary>
public class NodeLayoutTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public void NodeHeight_matches_the_previous_inline_formula(int choiceCount)
    {
        var node = new NodeViewModel("n", "text", 0, 0, NullEditorContext.Instance);
        for (var i = 0; i < choiceCount; i++)
            node.AddChoice();

        var expected = NodeLayout.ChoicesTop + choiceCount * NodeLayout.ChoiceRowHeight + 44;
        Assert.Equal(expected, NodeLayout.NodeHeight(node));
    }
}
