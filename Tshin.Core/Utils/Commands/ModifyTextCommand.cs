using System;
using Tshin.Core.Models;
using Tshin.Core.Utils.Managers;

namespace Tshin.Core.Utils.Commands;

/// <summary>
/// A data-mutation command responsible for assigning values to a specific <see cref="TextComponent"/> on an entity.
/// This command strictly supports direct text overwrites.
/// </summary>
public class ModifyTextCommand : ICommand
{
    /// <summary>
    /// Gets or sets the unique identifying name string of the target text component to modify.
    /// </summary>
    public string TargetComponentName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the string value to be assigned to the target text component.
    /// </summary>
    public string Value { get; set; } = string.Empty;
    // The specific execution behavior context (Increase, Reduce, or Set).

    public CommandField Field { get; set; } = CommandField.Set;
    
    // The target entity whose component data will be altered.
    public Entity Entity;


    /// <summary>
    /// Executes the assignment operation against the specified entity's text data component.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when an operation other than <see cref="CommandField.Set"/> is requested.</exception>
    public void Execute()
    {
        var textComp = EntityManager.GetComponent<TextComponent>(Entity, TargetComponentName);

        if (textComp is null) return;

        textComp.Value = Field == CommandField.Set
            ? Value
            : throw new InvalidOperationException($"Operation {Field} is unsupported for text components.");
    }
}