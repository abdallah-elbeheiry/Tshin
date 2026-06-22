using Tshin.Core.Models;

namespace Tshin.Core.Models;

public class StoryChoice : IChoice
{
    public INode? Node { get; set; }
    public string DisplayText { get; set; }

    public StoryChoice(INode? node, string displayText)
    {
        Node = node;
        DisplayText = displayText;
    }
}
