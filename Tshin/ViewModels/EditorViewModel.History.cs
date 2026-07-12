using System.Linq;
using CommunityToolkit.Mvvm.Input;
using Tshin.Models;

namespace Tshin.ViewModels;

/// <summary>
/// Snapshot-based undo/redo (partial of <see cref="EditorViewModel"/>). One logical action
/// is one entry: discrete ops close a boundary via <see cref="MarkDirty"/>, while continuous
/// runs (drags, typing) coalesce through <see cref="NoteContinuousChange"/> until flushed.
/// </summary>
public partial class EditorViewModel
{
    private readonly EditorHistory _history = new();

    // The last committed state; the "before" pushed onto the undo stack at each boundary.
    private HistoryEntry? _baseline;

    // Open continuous run (drag/typing), keyed by (target, kind) so a different edit flushes it.
    private bool _inContinuous;
    private object? _txnTarget;
    private string? _txnKind;

    // True while rebuilding the graph from a snapshot, so restore mutations don't record history.
    private bool _restoring;

    public bool CanUndo => _history.CanUndo;
    public bool CanRedo => _history.CanRedo;

    private HistoryEntry Capture() => new(BuildSnapshot(), ProjectName);

    /// <summary>Captures the initial baseline; call once after the graph is first built.</summary>
    private void InitHistory() => _baseline = Capture();

    public void MarkDirty()
    {
        IsDirty = true;
        if (_restoring) return;
        // A discrete change closes any open continuous run; the baseline already predates it,
        // so a single boundary captures both as one step (no empty/duplicate entry).
        _inContinuous = false;
        _txnTarget = null;
        _txnKind = null;
        CommitBoundary();
    }

    public void NoteContinuousChange(object target, string kind)
    {
        IsDirty = true;
        if (_restoring) return;
        if (_inContinuous && (!ReferenceEquals(_txnTarget, target) || _txnKind != kind))
            FlushHistory();
        _inContinuous = true;
        _txnTarget = target;
        _txnKind = kind;
    }

    public void FlushHistory()
    {
        if (!_inContinuous) return;
        _inContinuous = false;
        _txnTarget = null;
        _txnKind = null;
        CommitBoundary();
    }

    private void CommitBoundary()
    {
        if (_baseline is null)
        {
            _baseline = Capture();
            return;
        }
        _history.PushUndo(_baseline);
        _baseline = Capture();
        NotifyHistoryChanged();
    }

    private void NotifyHistoryChanged()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        FlushHistory();
        if (!_history.CanUndo) return;
        var sel = CaptureSelection();
        var target = _history.Undo(_baseline ?? Capture());
        ApplyHistory(target);
        RestoreSelection(sel);
        NotifyHistoryChanged();
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        FlushHistory();
        if (!_history.CanRedo) return;
        var sel = CaptureSelection();
        var target = _history.Redo(_baseline ?? Capture());
        ApplyHistory(target);
        RestoreSelection(sel);
        NotifyHistoryChanged();
    }

    private void ApplyHistory(HistoryEntry entry)
    {
        _restoring = true;

        foreach (var c in Connections) c.Dispose();
        Nodes.Clear();
        Entities.Clear();
        Connections.Clear();
        SelectedComponent = null;
        SelectedChoice = null;
        SelectedEntity = null;
        SelectedNode = null;

        BuildFrom(entry.Graph);
        ProjectName = entry.ProjectName;

        _restoring = false;
        _baseline = Capture();
        IsDirty = true;
    }

    // ---- selection preservation across a rebuild ----------------------------

    private readonly record struct SelectionRef(string Kind, string Owner, int Index);

    private SelectionRef CaptureSelection()
    {
        if (SelectedComponent is not null)
        {
            var owner = Entities.FirstOrDefault(e => e.Components.Contains(SelectedComponent));
            if (owner is not null)
                return new SelectionRef("component", owner.Id, owner.Components.IndexOf(SelectedComponent));
        }
        if (SelectedChoice is not null)
        {
            var owner = Nodes.FirstOrDefault(n => n.Choices.Contains(SelectedChoice));
            if (owner is not null)
                return new SelectionRef("choice", owner.Id, owner.Choices.IndexOf(SelectedChoice));
        }
        if (SelectedEntity is not null) return new SelectionRef("entity", SelectedEntity.Id, -1);
        if (SelectedNode is not null) return new SelectionRef("node", SelectedNode.Id, -1);
        return new SelectionRef("none", "", -1);
    }

    private void RestoreSelection(SelectionRef sel)
    {
        switch (sel.Kind)
        {
            case "node":
                SelectNode(Nodes.FirstOrDefault(n => n.Id == sel.Owner));
                break;
            case "entity":
                SelectEntity(Entities.FirstOrDefault(e => e.Id == sel.Owner));
                break;
            case "choice":
            {
                var n = Nodes.FirstOrDefault(x => x.Id == sel.Owner);
                if (n is not null && sel.Index >= 0 && sel.Index < n.Choices.Count)
                    SelectChoice(n.Choices[sel.Index]);
                break;
            }
            case "component":
            {
                var e = Entities.FirstOrDefault(x => x.Id == sel.Owner);
                if (e is not null && sel.Index >= 0 && sel.Index < e.Components.Count)
                    SelectComponent(e.Components[sel.Index]);
                break;
            }
        }
    }
}
