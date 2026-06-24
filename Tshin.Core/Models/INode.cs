namespace Tshin.Core.Models;

public interface INode
{
    string Id { get; set; }
    NodeType NodeType { get; }
    string DisplayText { get; set; }
    double X { get; set; }
    double Y { get; set; }
}

public interface IBranchingNode : INode{
    List<IChoice> Choices { get; set; }
}

