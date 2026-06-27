using Tshin.Core.Models;
using Tshin.Core.Utils.Commands;

namespace Tshin.Core.Utils.Managers;

public static class NodeManager
{
    internal static readonly Dictionary<string, INode> Nodes = [];

    internal static void AppendNode(INode node)
    {
        Nodes.Add(node.Id, node);
    }

    public static INode GetNode(string id) => Nodes.TryGetValue(id, out var node) ? node : null!;

    public static bool TryGetNode(string id, out INode? node) => Nodes.TryGetValue(id, out node);

    public static void RemoveNode(INode node) => Nodes.Remove(node.Id);

    public static void ClearNodes() => Nodes.Clear();

    public static List<string> GetNodeIds() => [.. Nodes.Keys];
    
    public static List<INode> GetNodes() => [.. Nodes.Values];
    
    public static int GetNodeCount() => Nodes.Count;

    public static void ModifyNodeText(string id, string text) => Nodes[id].DisplayText = text;

    public static void ModifyNodeId(string id, string newId)
    {
        if (id == newId) return;
        if (!Nodes.Remove(id, out var node)) return;
        
        // Handle collision: if newId already exists, append a suffix or just allow it to overwrite?
        // Actually, Dictionary.Add throws if it exists. 
        // Better to check if it exists and maybe return a bool or handle it.
        // For now, let's just make sure we don't crash and at least keep the node.
        if (Nodes.ContainsKey(newId))
        {
            // If it already exists, we have a problem. 
            // Let's try to make it unique by appending a suffix.
            string uniqueId = newId;
            int counter = 1;
            while (Nodes.ContainsKey(uniqueId))
            {
                uniqueId = $"{newId}_{counter++}";
            }
            newId = uniqueId;
        }
        
        node.Id = newId;
        Nodes.Add(newId, node);
    }

    public static void AddChoice(IChoice choice, IBranchingNode node) => node.Choices.Add(choice);
    public static void RemoveChoice(IChoice choice, IBranchingNode node) => node.Choices.Remove(choice);
    public static void ClearChoices(IBranchingNode node) => node.Choices.Clear();
    public static List<IChoice> GetChoices(IBranchingNode node) => node.Choices;
    public static IEnumerable<ICommand> GetChoiceCommands(IChoice choice) => choice.Commands;
    public static int GetChoiceCount(IBranchingNode node) => node.Choices.Count;
    public static IChoice GetChoice(IBranchingNode node, int index) => node.Choices[index];
    public static void ModifyChoiceText(IChoice choice, string text) => choice.DisplayText = text;
    public static void ModifyChoicePath(IChoice choice, INode? target) => choice.Node = target;
}