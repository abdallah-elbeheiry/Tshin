using System;
using System.Globalization;
using Tshin.Core.Models;
using Tshin.Core.Utils.Managers;

namespace Tshin.Core.Utils.Systems;

/// <summary>
/// A leaf node in the condition tree that compares a live entity's component
/// value against a stored target value using a specified operator.
/// The comparison is dispatched to <see cref="ComponentComparisonSystem"/>
/// after resolving the component's runtime type.
/// </summary>
public class AtomicConditionNode : IConditionComponentNode
{
    /// <summary>
    /// Gets or sets the entity identifier used to look up the target entity
    /// via <see cref="EntityManager"/>.
    /// </summary>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the component name used to retrieve the specific component
    /// from the target entity's component bag.
    /// </summary>
    public string ComponentId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the comparison operator.
    /// Supported values: "&gt;", "&lt;", "==", "!=", "&gt;=", "&lt;=".
    /// </summary>
    public string Operator { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target value as a raw string.
    /// On evaluation, this string is parsed into the appropriate type
    /// (double, bool, or string) based on the component's concrete type.
    /// </summary>
    public string TargetValue { get; set; } = string.Empty;

    /// <summary>
    /// Evaluates the atomic condition by resolving the entity and component
    /// from the manager, detecting the component's concrete type, converting
    /// <see cref="TargetValue"/> to the matching type, and delegating to
    /// <see cref="ComponentComparisonSystem"/>.
    /// </summary>
    /// <param name="entityManager">The active ECS entity manager.</param>
    /// <returns>
    /// <see langword="true"/> if the comparison succeeds;
    /// <see langword="false"/> if the entity or component is missing, or
    /// if type conversion fails.
    /// </returns>
    public bool Evaluate(EntityManager entityManager)
    {
        if (string.IsNullOrEmpty(EntityId) || string.IsNullOrEmpty(ComponentId) || string.IsNullOrEmpty(Operator))
            return false;

        if (!Guid.TryParse(EntityId, out var entityGuid))
            return false;

        var entity = entityManager.FindEntity(entityGuid);
        if (entity is null)
            return false;

        // Resolve the component and dispatch based on its concrete type
        var numberComp = entityManager.GetComponent<NumberComponent>(entity, ComponentId);
        if (numberComp is not null)
        {
            if (!double.TryParse(TargetValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                return false;

            var dummy = new NumberComponent { Value = parsed };
            return ComponentComparisonSystem.CompareNumbers(numberComp, dummy, Operator);
        }

        var textComp = entityManager.GetComponent<TextComponent>(entity, ComponentId);
        if (textComp is not null)
        {
            var dummy = new TextComponent { Value = TargetValue };
            return ComponentComparisonSystem.CompareText(textComp, dummy, Operator);
        }

        var condComp = entityManager.GetComponent<ConditionComponent>(entity, ComponentId);
        if (condComp is not null)
        {
            if (!bool.TryParse(TargetValue, out var parsed))
                return false;

            var dummy = new ConditionComponent { Value = parsed };
            return ComponentComparisonSystem.CompareConditions(condComp, dummy, Operator);
        }

        return false;
    }
}
