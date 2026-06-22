using Tshin.Core.Models;
using Tshin.Core.Utils;

namespace Tshin.Core;

public class Program
{
    public static void Main(string[] args)
    {
        for (var i = 0; i < 20; i++)
            NodeFactory.CreateNode(NodeType.StoryNode);
        
        FileWriter.SaveFileAsync("test.txt").Wait();
    }
}