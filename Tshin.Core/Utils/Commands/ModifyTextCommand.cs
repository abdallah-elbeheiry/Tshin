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

    /// <summary>
    /// Executes the assignment operation against the specified entity's text data component.
    /// </summary>
    /// <param name="entity">The target entity whose component data will be altered.</param>
    /// <param name="entityManager">The central manager handling component storage resolution.</param>
    /// <param name="command">The execution behavior context. Must be <see cref="CommandField.Set"/>.</param>
    /// <exception cref="InvalidOperationException">Thrown when an operation other than <see cref="CommandField.Set"/> is requested.</exception>
    public void Execute(Entity entity, EntityManager entityManager, CommandField command)
    {
        var textComp = entityManager.GetComponent<TextComponent>(entity, TargetComponentName);

        if (textComp is null) return;

        textComp.Value = command == CommandField.Set
            ? Value
            : throw new InvalidOperationException($"Operation {command} is unsupported for text components.");
    }
}