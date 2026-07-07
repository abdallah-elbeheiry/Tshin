using Tshin.Core.Utils.Managers;

namespace Tshin.Core.Models;

/// <summary>
/// Defines a node in a recursive condition evaluation tree.
/// Each node encapsulates a logical check — either an atomic comparison against
/// live entity component data, or a composite logical group (<c>and</c> / <c>or</c>)
/// that aggregates child nodes.
/// </summary>
public interface IConditionComponentNode
{
    /// <summary>
    /// Evaluates this condition node against the current ECS state.
    /// </summary>
    /// <param name="entityManager">
    /// The active entity manager from which component data is resolved.
    /// Implementations are expected to look up entities and components
    /// by the identifiers stored in the node.
    /// </param>
    /// <returns><see langword="true"/> if the condition represented by this
    /// node and its children is satisfied; otherwise, <see langword="false"/>.</returns>
    bool Evaluate(EntityManager entityManager);
}
