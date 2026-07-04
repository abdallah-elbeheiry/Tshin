using System.Linq;
using CommunityToolkit.Mvvm.Input;

namespace Tshin.ViewModels;

/// <summary>
/// Selection management for the editor (partial of <see cref="EditorViewModel"/>).
/// </summary>
public partial class EditorViewModel
{
    public void SelectNode(NodeViewModel? node)
    {
        if (SelectedNode == node) return;
        if (SelectedNode is not null) SelectedNode.IsSelected = false;
        SelectedNode = node;
        if (node is not null) node.IsSelected = true;
        // Deselect choice/component when selecting a node
        SelectedChoice = null;
        SelectedEntity = null;
        SelectedComponent = null;
    }

    public void SelectChoice(ChoiceViewModel? choice)
    {
        SelectedChoice = choice;
        SelectedNode = null;
        SelectedEntity = null;
        SelectedComponent = null;
    }

    public void SelectEntity(EntityViewModel? entity)
    {
        SelectedEntity = entity;
        SelectedNode = null;
        SelectedChoice = null;
        SelectedComponent = null;
    }

    public void SelectComponent(ComponentViewModel? component)
    {
        // Anchor selection on the component's owning entity so the "Back to entity"
        // breadcrumb returns there instead of whichever inspector was last open.
        if (component is not null)
        {
            var owner = Entities.FirstOrDefault(e => e.Components.Contains(component));
            if (owner is not null) SelectedEntity = owner;
        }
        SelectedChoice = null;
        SelectedNode = null;
        SelectedComponent = component;
    }

    [RelayCommand]
    private void OpenChoiceInspector(ChoiceViewModel? choice)
    {
        SelectChoice(choice);
    }
}