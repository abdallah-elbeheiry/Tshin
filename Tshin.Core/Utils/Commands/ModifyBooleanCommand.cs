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
    // The specific execution behavior context (Increase, Reduce, or Set).

    public CommandField Field { get; set; } = CommandField.Set;

    // The target entity whose component data will be altered.
    public Entity Entity;


    /// <summary>
    /// Executes the direct assignment operation against the specified entity's condition data component.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when an operation other than <see cref="CommandField.Set"/> is requested.</exception>
    public void Execute()
    {
        var boolComp = EntityManager.GetComponent<ConditionComponent>(Entity, TargetComponentName);

        if (boolComp is null) return;

        boolComp.Value = Field == CommandField.Set
            ? Value
            : throw new InvalidOperationException($"Unknown command field {Field}");
    }
}