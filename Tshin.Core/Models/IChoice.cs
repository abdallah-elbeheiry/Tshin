using System.Collections.Generic;
using Tshin.Core.Utils.Commands;

namespace Tshin.Core.Models;

/// <summary>
/// Defines the contract for a narrative choice or branching path within the story graph.
/// </summary>
public interface IChoice
{
    /// <summary>
    /// Gets or sets the target destination node to navigate to when this choice is selected.
    /// A value of <see langword="null"/> indicates an ending path or a terminal choice.
    /// </summary>
    INode? Node { get; set; }

    /// <summary>
    /// Gets or sets the reader-facing text or label displayed on screen for this option.
    /// </summary>
    string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the optional root condition node that gates this choice.
    /// When non-null, the choice is only available if <see cref="IConditionComponentNode.Evaluate"/>
    /// returns <see langword="true"/> against the current ECS state.
    /// A value of <see langword="null"/> means no requirement — the choice is always available.
    /// </summary>
    IConditionComponentNode? Condition { get; set; }

    /// <summary>
    /// Gets or sets the collection of side-effect mutations that must execute in order when the path is chosen.
    /// </summary>
    List<ICommand> Commands { get; set; }
}
