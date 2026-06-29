using System;
using Tshin.Core.Models;
using Tshin.Core.Utils.Managers;

namespace Tshin.Core.Utils.Commands;

/// <summary>
/// A data-mutation command responsible for modifying a specific <see cref="NumberComponent"/> on an entity.
/// All modifications are strictly clamped between the component's minimum and maximum value bounds.
/// </summary>
public class ModifyNumberCommand : ICommand
{
    /// <summary>
    /// Gets or sets the unique identifying name string of the target number component to modify.
    /// </summary>
    public string TargetComponentName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the numerical value used as the modifier or direct assignment operand.
    /// </summary>
    public double Value { get; set; }
    
    /// <summary>
    /// Gets or sets the target entity whose component data will be altered.
    /// </summary>
    public Entity Entity { get; set; } = null!;
    
    // The specific execution behavior context (Increase, Reduce, or Set).
    public CommandField Field { get; set; } = CommandField.Set;

    /// <summary>
    /// Executes the bounded numerical operation against the specified entity's data component.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when an unsupported or unknown <see cref="CommandField"/> value is passed.</exception>
    public void Execute(EntityManager entityManager)
    {
        var numComp = entityManager.GetComponent<NumberComponent>(Entity, TargetComponentName);

        if (numComp is null) return;

        numComp.Value = Field switch
        {
            CommandField.Increase => Math.Clamp(numComp.Value + Value, numComp.MinValue, numComp.MaxValue),
            CommandField.Reduce => Math.Clamp(numComp.Value - Value, numComp.MinValue, numComp.MaxValue),
            CommandField.Set => Math.Clamp(Value, numComp.MinValue, numComp.MaxValue),
            _ => throw new InvalidOperationException($"Unknown command field {Field}")
        };
    }
}
