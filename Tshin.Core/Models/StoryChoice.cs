using Tshin.Core.Models;

namespace Tshin.Core.Models;

public record StoryChoice(INode Node, string DisplayText) : IChoice
{
    public INode Node { get; set; } = Node;
    public string DisplayText { get; set; } = DisplayText;
}
