using Tshin.Core.Models;

namespace Tshin.Core.Utils.Systems;

/// <summary>
/// A pure ECS system responsible for executing logic and comparison operations across data components.
/// </summary>
public static class ComponentComparisonSystem
{
    /// <summary>
    /// Compares the values of two <see cref="NumberComponent"/> instances using a specified mathematical operator string.
    /// </summary>
    /// <param name="left">The left-hand component data containing the primary value.</param>
    /// <param name="right">The right-hand component data containing the value to compare against.</param>
    /// <param name="op">The comparison operator string. Supported values: "&gt;", "&lt;", "==", "!=", "&gt;=", "&lt;=".</param>
    /// <returns><see langword="true"/> if the conditional comparison evaluates to true; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when an invalid or unsupported operator string is provided.</exception>
    public static bool CompareNumbers(NumberComponent left, NumberComponent right, string op)
    {
        return op switch
        {
            ">" => left.Value > right.Value,
            "<" => left.Value < right.Value,
            "==" => left.Value.Equals(right.Value),
            "!=" => !left.Value.Equals(right.Value),
            ">=" => left.Value >= right.Value,
            "<=" => left.Value <= right.Value,
            _ => throw new InvalidOperationException($"Unknown operator {op}")
        };
    }
}