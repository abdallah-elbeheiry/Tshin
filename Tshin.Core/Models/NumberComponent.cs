namespace Tshin.Core.Models;

/// <summary>
/// A NumberComponent is a component that holds a number (double).
/// Name serves as a unique identifier for the component.
/// Value is the actual number used by the component.
/// MinValue and MaxValue are the min and max values the component can hold, they can be adjusted by the user (Not implemented yet).
/// See <see cref="IComponent"/> for more info.
/// </summary>
public class NumberComponent : IComponent
{
    public string Name { get; set; } = "New Number";

    public double Value { get; set; }
    public double MinValue { get; set; } = 0;
    public double MaxValue { get; set; } = double.MaxValue;
}