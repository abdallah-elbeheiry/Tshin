namespace Tshin.Core.Models;

/// <summary>
/// is a <see cref="IComponent"/> that can be compared to other components (bigger than, smaller than, etc).
/// </summary>
public interface IComparableComponent : IComponent
{
    bool BiggerThan(IComponent other);
    bool SmallerThan(IComponent other);
    bool BiggerThanOrEquals(IComponent other);
    bool SmallerThanOrEquals(IComponent other);
}