using System.Collections.Generic;
using Tshin.Models;

namespace Tshin.ViewModels;

/// <summary>One point-in-time state on the undo/redo timeline.</summary>
public sealed record HistoryEntry(StorySnapshot Graph, string ProjectName);

/// <summary>
/// In-memory, session-scoped undo/redo stacks for the editor. Holds full-graph snapshots;
/// one logical user action = one entry (see <see cref="EditorViewModel"/> commit boundaries).
/// </summary>
public sealed class EditorHistory
{
    private readonly Stack<HistoryEntry> _undo = new();
    private readonly Stack<HistoryEntry> _redo = new();

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Records a new committed step; a fresh action invalidates the redo timeline.</summary>
    public void PushUndo(HistoryEntry before)
    {
        _undo.Push(before);
        _redo.Clear();
    }

    /// <summary>Pops the last undo state, banking <paramref name="current"/> for redo.</summary>
    public HistoryEntry Undo(HistoryEntry current)
    {
        var target = _undo.Pop();
        _redo.Push(current);
        return target;
    }

    /// <summary>Pops the last redo state, banking <paramref name="current"/> for undo.</summary>
    public HistoryEntry Redo(HistoryEntry current)
    {
        var target = _redo.Pop();
        _undo.Push(current);
        return target;
    }
}
