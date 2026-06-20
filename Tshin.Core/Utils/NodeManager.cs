using Tshin.Core.Models;

namespace Tshin.Core.Utils;

public static class NodeManager
{
    internal static readonly List<INode> Nodes = [];

    internal static void AppendNode(INode node) => Nodes.Add(node);

    public static INode GetNode(string id) => Nodes.First(x => x.Id == id);
    
    public static void RemoveNode(INode node) => Nodes.Remove(node);
    
    public static void ClearNodes() => Nodes.Clear();
    
    public static List<string> GetNodeIds() => Nodes.Select(x => x.Id).ToList();
    public static List<INode> GetNodes() => Nodes;
    public static int GetNodeCount() => Nodes.Count;
    
    public static void ModifyNodeText(string id, string text) => Nodes.First(x => x.Id == id).DisplayText = text;
    public static void ModifyNodeId(string id, string newId) => Nodes.First(x => x.Id == id).Id = newId;

    public static void AddChoice(IChoice choice, IBranchingNode node) => node.Choices.Add(choice);
    public static void RemoveChoice(IChoice choice, IBranchingNode node) => node.Choices.Remove(choice);
    public static void ClearChoices(IBranchingNode node) => node.Choices.Clear();
    public static List<IChoice> GetChoices(IBranchingNode node) => node.Choices;
    public static int GetChoiceCount(IBranchingNode node) => node.Choices.Count;
    public static IChoice GetChoice(IBranchingNode node, int index) => node.Choices[index];
    public static void ModifyChoiceText(IChoice choice, string text) => choice.DisplayText = text;
    public static void ModifyChoicePath(IChoice choice, INode target) => choice.Node = target;


}