using System.Linq;
using CommunityToolkit.Mvvm.Input;

namespace Tshin.ViewModels;

/// <summary>
/// Node/choice/command editing operations (partial of <see cref="EditorViewModel"/>).
/// </summary>
public partial class EditorViewModel
{
    public NodeViewModel CreateNodeAt(double worldX, double worldY)
    {
        var id = NextNodeId();
        var node = new NodeViewModel(id, "New node", worldX, worldY, MarkDirty);
        Nodes.Add(node);
        SelectNode(node);
        MarkDirty();
        return node;
    }

    private string NextNodeId()
    {
        string id;
        do { id = $"node_{++_newNodeCounter}"; }
        while (Nodes.Any(n => n.Id == id));
        return id;
    }

    [RelayCommand]
    private void RemoveNode(NodeViewModel? node)
    {
        node ??= SelectedNode;
        if (node is null) return;

        // Drop any choices pointing at the removed node.
        foreach (var other in Nodes)
        foreach (var choice in other.Choices.Where(c => c.Target == node).ToList())
            choice.Target = null;

        Nodes.Remove(node);
        if (SelectedNode == node) SelectedNode = null;
        RebuildConnections();
        MarkDirty();
    }

    [RelayCommand]
    private void AddChoice(NodeViewModel? node)
    {
        node ??= SelectedNode;
        if (node is null) return;
        var choice = new ChoiceViewModel("New choice", null, MarkDirty);
        choice.AvailableEntities = Entities;
        node.Choices.Add(choice);
        RebuildConnections();
        MarkDirty();
    }

    [RelayCommand]
    private void RemoveChoice(ChoiceViewModel? choice)
    {
        if (choice is null) return;
        var owner = Nodes.FirstOrDefault(n => n.Choices.Contains(choice));
        if (owner is null) return;
        owner.Choices.Remove(choice);
        if (SelectedChoice == choice) SelectedChoice = null;
        RebuildConnections();
        MarkDirty();
    }

    [RelayCommand]
    private void AddCommandToChoice(ChoiceViewModel? choice)
    {
        if (choice is null) return;
        var cmdVm = new CommandViewModel(
            null, "", "Set", "", 0, false, Entities, MarkDirty);
        choice.Commands.Add(cmdVm);
        MarkDirty();
    }

    [RelayCommand]
    private void RemoveCommandFromChoice(CommandViewModel? command)
    {
        if (command is null) return;
        // Find the choice that owns this command
        foreach (var node in Nodes)
        {
            foreach (var choice in node.Choices)
            {
                if (choice.Commands.Remove(command))
                {
                    MarkDirty();
                    return;
                }
            }
        }
    }

    /// <summary>Links a choice to a target node (drag-to-connect or inspector).</summary>
    public void Connect(ChoiceViewModel choice, NodeViewModel target)
    {
        if (choice.Target == target) return;
        choice.Target = target;
        RebuildConnections();
        MarkDirty();
    }

    public void RebuildConnections()
    {
        foreach (var c in Connections) c.Dispose();
        Connections.Clear();

        foreach (var node in Nodes)
        {
            for (var i = 0; i < node.Choices.Count; i++)
            {
                var target = node.Choices[i].Target;
                if (target is not null)
                    Connections.Add(new ConnectionViewModel(node, target, i));
            }
        }
    }
}