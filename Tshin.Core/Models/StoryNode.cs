namespace Tshin.Core.Models;

public class StoryNode(string id, NodeType nodeType, string displayText, List<IChoice> choices, double x, double y)
    : IBranchingNode
{
    public string Id { get; set; } = id;
    public NodeType NodeType { get; } = nodeType;
    public string DisplayText { get; set; } = displayText;
    public List<IChoice> Choices { get; set; } = choices;
    public double X { get; set; } = x;
    public double Y { get; set; } = y;
}
