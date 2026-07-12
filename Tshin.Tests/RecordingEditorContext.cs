using System.Collections.ObjectModel;
using Tshin.ViewModels;

namespace Tshin.Tests;

/// <summary>
/// Test double for <see cref="IEditorContext"/> that records how many times a child VM
/// signalled a change (via <see cref="MarkDirty"/>). All editor operations are no-ops.
/// </summary>
internal sealed class RecordingEditorContext : IEditorContext
{
    public int MarkDirtyCount { get; private set; }

    public ObservableCollection<EntityViewModel> Entities { get; } = new();

    public void MarkDirty() => MarkDirtyCount++;
    public void NoteContinuousChange(object target, string kind) => MarkDirtyCount++;
    public void FlushHistory() { }
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
