namespace Tshin.Core.Models;

/// <summary>
/// A NumberComponent is a component that holds a number (double).
/// Id serves as a unique identifier for the component.
/// Value is the actual number used by the component.
/// MinValue and MaxValue are the min and max values the component can hold, they can be adjusted by the user (Not implemented yet).
/// See <see cref="IComparableComponent"/> for more info.
/// </summary>
public class NumberComponent : IComparableComponent
{
    public string Id { get; set; } = "New Number";
    public ComponentType Type => ComponentType.Number;

    public double Value { get; set; }
    public double MinValue { get; set; } = 0;
    public double MaxValue { get; set; } = double.MaxValue;

    public bool Equals(IComponent? other)
    {
        return other switch
        {
            NumberComponent n => Value.Equals(n.Value),
            _ => false
        };
    }

    public bool BiggerThan(IComponent other)
    {
        return other switch
        {
            NumberComponent n => Value > n.Value,
            null => throw new ArgumentNullException(nameof(other), "Cannot compare with a null component."),
            _ => throw new ArgumentException($"Invalid component types: you were trying to compare {GetType().Name} with {other.GetType().Name}")
        };
    }

    public bool SmallerThan(IComponent other)
    {
        return other switch
        {
            NumberComponent n => Value < n.Value,
            null => throw new ArgumentNullException(nameof(other), "Cannot compare with a null component."),
            _ => throw new ArgumentException($"Invalid component types: you were trying to compare {GetType().Name} with {other.GetType().Name}")
        };
    }

    public bool BiggerThanOrEquals(IComponent other) => BiggerThan(other) || Equals(other);

    public bool SmallerThanOrEquals(IComponent other) => SmallerThan(other) || Equals(other);
}