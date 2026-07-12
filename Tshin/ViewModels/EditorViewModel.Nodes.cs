using System.Linq;
using CommunityToolkit.Mvvm.Input;

namespace Tshin.ViewModels;

/// <summary>
/// Node/choice/command editing operations (partial of <see cref="EditorViewModel"/>).
/// Most operate on a child VM and are invoked through <see cref="IEditorContext"/> from the
/// child's own command; a few keep a generated command for the toolbar/keyboard.
/// </summary>
public partial class EditorViewModel
{
    public NodeViewModel CreateNodeAt(double worldX, double worldY)
    {
        var id = NextNodeId();
        var node = new NodeViewModel(id, "New node", worldX, worldY, this);
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
    public void RemoveNode(NodeViewModel? node)
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

    public void AddChoice(NodeViewModel? node)
    {
        node ??= SelectedNode;
        if (node is null) return;
        var choice = new ChoiceViewModel("New choice", null, this);
        choice.AvailableEntities = Entities;
        node.Choices.Add(choice);
        RebuildConnections();
        MarkDirty();
    }

    public void RemoveChoice(ChoiceViewModel? choice)
    {
        if (choice is null) return;
        var owner = Nodes.FirstOrDefault(n => n.Choices.Contains(choice));
        if (owner is null) return;
        owner.Choices.Remove(choice);
        if (SelectedChoice == choice) SelectedChoice = null;
        RebuildConnections();
        MarkDirty();
    }

    public void AddCommandToChoice(ChoiceViewModel? choice)
    {
        if (choice is null) return;
        var cmdVm = new CommandViewModel(
            null, "", "Set", "", 0, false, Entities, this);
        choice.Commands.Add(cmdVm);
        MarkDirty();
    }

    public void RemoveCommandFromChoice(CommandViewModel? command)
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

    // ---- condition editing --------------------------------------------------

    public void AddConditionToChoice(ChoiceViewModel? choice)
    {
        if (choice is null) return;
        var root = new LogicalGroupViewModel(this) { AvailableEntities = Entities };
        root.AddChild(new AtomicConditionViewModel(null, Entities, this));
        choice.ConditionRoot = root;
        MarkDirty();
    }

    public void RemoveCondition(ChoiceViewModel? choice)
    {
        if (choice is null) return;
        choice.ConditionRoot = null;
        MarkDirty();
    }

    public void AddConditionToCommand(CommandViewModel? command)
    {
        if (command is null) return;
        var root = new LogicalGroupViewModel(this) { AvailableEntities = command.AvailableEntities };
        root.AddChild(new AtomicConditionViewModel(null, command.AvailableEntities, this));
        command.ConditionRoot = root;
        MarkDirty();
    }

    public void RemoveConditionFromCommand(CommandViewModel? command)
    {
        if (command is null) return;
        command.ConditionRoot = null;
        MarkDirty();
    }

    public void AddAtomicToGroup(LogicalGroupViewModel? group)
    {
        if (group is null) return;
        group.AddChild(new AtomicConditionViewModel(null, group.AvailableEntities ?? Entities, this));
        MarkDirty();
    }

    public void AddGroupToGroup(LogicalGroupViewModel? group)
    {
        if (group is null) return;
        var child = new LogicalGroupViewModel(this) { AvailableEntities = group.AvailableEntities ?? Entities };
        child.AddChild(new AtomicConditionViewModel(null, child.AvailableEntities, this));
        group.AddChild(child);
        MarkDirty();
    }

    public void RemoveConditionNode(ConditionNodeViewModel? node)
    {
        if (node is null) return;

        var parent = node.Parent;
        if (parent is not null)
            parent.RemoveChild(node);

        // Walk up to the tree root, then find the choice that owns it. Use the captured
        // parent — RemoveChild has already cleared node.Parent by now.
        ConditionNodeViewModel root = parent ?? node;
        while (root.Parent is not null)
            root = root.Parent;

        // Removing the root node itself, or emptying the root group, clears the condition
        // on whichever owner (choice or command) holds this tree.
        if (parent is null || (root is LogicalGroupViewModel g && g.Children.Count == 0))
            ClearConditionRootOwner(root);

        MarkDirty();
    }

    /// <summary>Nulls the <c>ConditionRoot</c> of the choice or command that owns <paramref name="root"/>.</summary>
    private void ClearConditionRootOwner(ConditionNodeViewModel root)
    {
        foreach (var node in Nodes)
            foreach (var choice in node.Choices)
            {
                if (ReferenceEquals(choice.ConditionRoot, root))
                {
                    choice.ConditionRoot = null;
                    return;
                }
                foreach (var command in choice.Commands)
                    if (ReferenceEquals(command.ConditionRoot, root))
                    {
                        command.ConditionRoot = null;
                        return;
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

    /// <summary>Unlinks a choice from its target node (drag its wire to empty canvas).</summary>
    public void Disconnect(ChoiceViewModel choice)
    {
        if (choice.Target is null) return;
        choice.Target = null;
        RebuildConnections();
        MarkDirty();
    }

    public void RebuildConnections()
    {
        foreach (var c in Connections) c.Dispose();
        Connections.Clear();

        // Deterministic colour index: a running counter in stable (node, choice) order,
        // so the same graph always yields the same wire colours (no static leak).
        var colorIndex = 0;
        foreach (var node in Nodes)
        {
            for (var i = 0; i < node.Choices.Count; i++)
            {
                var target = node.Choices[i].Target;
                if (target is not null)
                    Connections.Add(new ConnectionViewModel(node, target, i, colorIndex++));
            }
        }
    }
}
