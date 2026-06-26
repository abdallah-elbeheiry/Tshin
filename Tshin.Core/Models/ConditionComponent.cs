namespace Tshin.Core.Models;

/// <summary>
/// A condition component is a component that holds a boolean value.
/// Id serves as a unique identifier for the component.
/// Value is the actual boolean used by the component.
/// Planned to be used for user logic control, i.e if character is dead or rich
/// more functionality will be added later.
/// See <see cref="IComponent"/> for more info.
/// </summary>
public class ConditionComponent : IComponent
{
    public string Id { get; set; } = "New Condition";
    public ComponentType Type => ComponentType.Condition;
    public bool Value { get; set; }

    public bool Equals(IComponent? other)
    {
        return other switch
        {
            ConditionComponent c => Value.Equals(c.Value),
            _ => false
        };
    }
}