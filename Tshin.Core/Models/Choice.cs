namespace Tshin.Core.Models;

public class Choice : IChoice
{
    public INode? Node { get; set; }
    public string DisplayText { get; set; }

    public Choice(INode? node, string displayText)
    {
        Node = node;
        DisplayText = displayText;
    }
}