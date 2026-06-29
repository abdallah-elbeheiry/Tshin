using Tshin.Core.Models;
using Tshin.Core.Utils.Commands;

namespace Tshin.Core.Utils.Managers;

public class NodeManager
{
    public readonly Dictionary<string, INode> Nodes = [];

    public void AppendNode(INode node)
    {
        Nodes.Add(node.Id, node);
    }

    public INode GetNode(string id) => Nodes.TryGetValue(id, out var node) ? node : null!;

    public bool TryGetNode(string id, out INode? node) => Nodes.TryGetValue(id, out node);

    public void RemoveNode(INode node) => Nodes.Remove(node.Id);

    public void ClearNodes() => Nodes.Clear();

    public List<string> GetNodeIds() => [.. Nodes.Keys];
    
    public List<INode> GetNodes() => [.. Nodes.Values];
    
    public int GetNodeCount() => Nodes.Count;

    public void ModifyNodeText(string id, string text) => Nodes[id].DisplayText = text;

    public void ModifyNodeId(string id, string newId)
    {
        if (id == newId) return;
        if (!Nodes.Remove(id, out var node)) return;
        
        if (Nodes.ContainsKey(newId))
        {
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

    public void AddChoice(IChoice choice, IBranchingNode node) => node.Choices.Add(choice);
    public void RemoveChoice(IChoice choice, IBranchingNode node) => node.Choices.Remove(choice);
    public void ClearChoices(IBranchingNode node) => node.Choices.Clear();
    public List<IChoice> GetChoices(IBranchingNode node) => node.Choices;
    public int GetChoiceCount(IBranchingNode node) => node.Choices.Count;
    public IChoice GetChoice(IBranchingNode node, int index) => node.Choices[index];
    public void ModifyChoiceText(IChoice choice, string text) => choice.DisplayText = text;
    public void ModifyChoicePath(IChoice choice, INode? target) => choice.Node = target;
}