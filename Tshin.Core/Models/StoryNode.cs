namespace Tshin.Core.Models;

public sealed record StoryNode(string Id, NodeType NodeType, string DisplayText, List<IChoice> Choices)
    : IBranchingNode
{
    public string Id { get; set; } = Id;
    public NodeType NodeType { get; } = NodeType;
    public string DisplayText { get; set; } = DisplayText;
    public List<IChoice> Choices { get; set; } = Choices;
}