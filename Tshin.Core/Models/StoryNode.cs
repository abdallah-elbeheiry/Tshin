namespace Tshin.Core.Models;

public class StoryNode : IBranchingNode
{
    public string Id { get; set; }
    public NodeType NodeType { get; }
    public string DisplayText { get; set; }
    public List<IChoice> Choices { get; set; }

    public StoryNode(string id, NodeType nodeType, string displayText, List<IChoice> choices)
    {
        Id = id;
        NodeType = nodeType;
        DisplayText = displayText;
        Choices = choices;
    }
}