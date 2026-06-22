namespace Tshin.Core.Models;

public sealed record Choice(INode Node, string DisplayText) : IChoice
{
    public INode Node { get; set; } = Node;
    public string DisplayText { get; set; } = DisplayText;
}