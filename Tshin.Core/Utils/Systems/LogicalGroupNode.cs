using System.Collections.Generic;
using System.Linq;
using Tshin.Core.Models;
using Tshin.Core.Utils.Managers;

namespace Tshin.Core.Utils.Systems;

/// <summary>
/// A composite condition node that aggregates child <see cref="IConditionComponentNode"/>
/// instances under a logical operator — either conjunction (<c>and</c>) or
/// disjunction (<c>or</c>). Evaluation short-circuits using LINQ
/// <see cref="Enumerable.All"/> or <see cref="Enumerable.Any"/>.
/// </summary>
public class LogicalGroupNode : IConditionComponentNode
{
    /// <summary>
    /// Gets or sets the collection of child condition nodes to evaluate.
    /// </summary>
    public List<IConditionComponentNode> Children { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether this group represents a
    /// logical <c>and</c> (conjunction). When <see langword="false"/>, the
    /// group represents a logical <c>or</c> (disjunction).
    /// </summary>
    public bool IsAnd { get; set; } = true;

    /// <summary>
    /// Evaluates all child nodes using short-circuiting logic:
    /// <c>and</c> groups return <see langword="true"/> only when every child
    /// evaluates to <see langword="true"/>; <c>or</c> groups return
    /// <see langword="true"/> when at least one child evaluates to
    /// <see langword="true"/>.
    /// </summary>
    /// <param name="entityManager">The active ECS entity manager.</param>
    /// <returns>
    /// The aggregate boolean result of all child evaluations according to
    /// the group's logical operator.
    /// </returns>
    public bool Evaluate(EntityManager entityManager)
    {
        if (Children.Count == 0)
            return IsAnd;

        return IsAnd
            ? Children.All(c => c.Evaluate(entityManager))
            : Children.Any(c => c.Evaluate(entityManager));
    }
}
