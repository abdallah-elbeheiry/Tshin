using System;
using Tshin.Core.Models;
using Tshin.Core.Utils.Managers;

namespace Tshin.Core.Utils.Commands;

/// <summary>
/// A data-mutation command responsible for assigning state values to a specific boolean-backed <see cref="ConditionComponent"/> on an entity.
/// This command strictly supports direct state overwrites.
/// </summary>
public class ModifyBooleanCommand : ICommand
{
    /// <summary>
    /// Gets or sets the unique identifying name string of the target condition component to modify.
    /// </summary>
    public string TargetComponentName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target boolean truth value to be assigned to the component.
    /// </summary>
    public bool Value { get; set; }

    /// <summary>
    /// Executes the direct assignment operation against the specified entity's condition data component.
    /// </summary>
    /// <param name="entity">The target entity whose component data will be altered.</param>
    /// <param name="entityManager">The central manager handling component storage resolution.</param>
    /// <param name="command">The execution behavior context. Must be <see cref="CommandField.Set"/>.</param>
    /// <exception cref="InvalidOperationException">Thrown when an operation other than <see cref="CommandField.Set"/> is requested.</exception>
    public void Execute(Entity entity, EntityManager entityManager, CommandField command)
    {
        var boolComp = entityManager.GetComponent<ConditionComponent>(entity, TargetComponentName);

        if (boolComp is null) return;

        boolComp.Value = command == CommandField.Set
            ? Value
            : throw new InvalidOperationException($"Unknown command field {command}");
    }
}