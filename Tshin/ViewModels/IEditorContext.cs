using System.Collections.ObjectModel;

namespace Tshin.ViewModels;

/// <summary>
/// The editor operations a child view model may invoke on its owning editor. Injected into
/// every editable child VM in place of a bare change callback, so child views can bind to
/// their own commands (e.g. <c>RemoveSelfCommand</c>) instead of reaching up the visual tree
/// to the <see cref="EditorViewModel"/>. <see cref="EditorViewModel"/> implements it;
/// play-mode clones use <see cref="NullEditorContext"/>.
/// </summary>
public interface IEditorContext
{
    /// <summary>Marks the editor dirty; also the low-level change signal for child VMs.</summary>
    void MarkDirty();

    /// <summary>All entities, shared with choice/command/condition pickers.</summary>
    ObservableCollection<EntityViewModel> Entities { get; }

    void AddChoice(NodeViewModel? node);
    void RemoveNode(NodeViewModel? node);
    void RemoveChoice(ChoiceViewModel? choice);
    void SelectChoice(ChoiceViewModel? choice);
    void AddCommandToChoice(ChoiceViewModel? choice);
    void RemoveCommandFromChoice(CommandViewModel? command);
    void AddConditionToChoice(ChoiceViewModel? choice);
    void RemoveCondition(ChoiceViewModel? choice);
    void AddConditionToCommand(CommandViewModel? command);
    void RemoveConditionFromCommand(CommandViewModel? command);
    void AddAtomicToGroup(LogicalGroupViewModel? group);
    void AddGroupToGroup(LogicalGroupViewModel? group);
    void RemoveConditionNode(ConditionNodeViewModel? node);
    void RemoveComponentFromEntity(ComponentViewModel? component);
    void RemoveEntity(EntityViewModel? entity);
    void BackToEntity();
}

/// <summary>
/// No-op context for view models built outside an editor (play-mode entity/component clones).
/// Their editing commands are never surfaced, so every operation is a safe no-op.
/// </summary>
public sealed class NullEditorContext : IEditorContext
{
    public static readonly NullEditorContext Instance = new();

    public ObservableCollection<EntityViewModel> Entities { get; } = new();

    public void MarkDirty() { }
    public void AddChoice(NodeViewModel? node) { }
    public void RemoveNode(NodeViewModel? node) { }
    public void RemoveChoice(ChoiceViewModel? choice) { }
    public void SelectChoice(ChoiceViewModel? choice) { }
    public void AddCommandToChoice(ChoiceViewModel? choice) { }
    public void RemoveCommandFromChoice(CommandViewModel? command) { }
    public void AddConditionToChoice(ChoiceViewModel? choice) { }
    public void RemoveCondition(ChoiceViewModel? choice) { }
    public void AddConditionToCommand(CommandViewModel? command) { }
    public void RemoveConditionFromCommand(CommandViewModel? command) { }
    public void AddAtomicToGroup(LogicalGroupViewModel? group) { }
    public void AddGroupToGroup(LogicalGroupViewModel? group) { }
    public void RemoveConditionNode(ConditionNodeViewModel? node) { }
    public void RemoveComponentFromEntity(ComponentViewModel? component) { }
    public void RemoveEntity(EntityViewModel? entity) { }
    public void BackToEntity() { }
}
