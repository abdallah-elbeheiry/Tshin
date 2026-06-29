using System.Collections.Generic;
using Tshin.Core.Utils.Commands;

namespace Tshin.Core.Models;

/// <summary>
/// Represents a concrete narrative choice or branch within a story node.
/// Each choice can optionally hold a sequence of commands to execute side-effects when selected.
/// Commands should all be run when the player selects this choice.
/// </summary>
/// <param name="node">The target destination node to navigate to when this choice is selected.</param>
/// <param name="displayText">The localized or raw text displayed to the player for this choice.</param>
/// <param name="commands">The collection of mutations to run against game data when this choice is executed.</param>
public class Choice(INode? node, string displayText, List<ICommand> commands)
    : IChoice
{
    /// <summary>
    /// Gets or sets the target destination node that this choice transitions the story to.
    /// A value of <see langword="null"/> indicates an ending path or a terminal choice.
    /// </summary>
    public INode? Node { get; set; } = node;

    /// <summary>
    /// Gets or sets the player-facing text displayed on the screen for this option.
    /// </summary>
    public string DisplayText { get; set; } = displayText;

    /// <summary>
    /// Gets or sets the collection of commands that execute sequentially when the player selects this path.
    /// </summary>
    public List<ICommand> Commands { get; set; } = commands;

    /// <summary>
    /// Initializes a new instance of the <see cref="Choice"/> class with display text only,
    /// containing no target node or accompanying state mutations.
    /// The <see cref="Node"/> property starts as <see langword="null"/> and should be set
    /// explicitly after creation.
    /// </summary>
    /// <param name="displayText">The text displayed to the player.</param>
    public Choice(string displayText) : this(null, displayText, [])
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Choice"/> class with a destination node and text,
    /// containing no accompanying state mutations.
    /// </summary>
    /// <param name="node">The target destination node.</param>
    /// <param name="displayText">The text displayed to the player.</param>
    public Choice(INode? node, string displayText) : this(node, displayText, [])
    {
    }
}
