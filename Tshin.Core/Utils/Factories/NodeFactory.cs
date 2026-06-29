using Tshin.Core.Models;

namespace Tshin.Core.Utils.Factories;

public class NodeFactory
{
    public static INode CreateNode(NodeType type, string? id = null, double x = 0, double y = 0)
    {
        switch (type)
        {
            case NodeType.StoryNode:
                return new StoryNode(id ?? Guid.NewGuid().ToString(), NodeType.StoryNode, "", [], x, y);
            default:
                throw new NotImplementedException("Node type not implemented: " + type);
        }
    }
}