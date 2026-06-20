using Tshin.Core.Models;

namespace Tshin.Core.Utils;

public class NodeFactory
{
    public static INode CreateNode(NodeType type)
    {
        switch (type)
        {
            case NodeType.StoryNode:
                var node = new StoryNode(Guid.NewGuid().ToString(), NodeType.StoryNode, "", []);
                NodeManager.AppendNode(node);
                return node;
            default:
                throw new NotImplementedException("Node type not implemented: " + type);
        }
    }
}