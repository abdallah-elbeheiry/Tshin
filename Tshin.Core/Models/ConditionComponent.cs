namespace Tshin.Core.Models;

/// <summary>
/// A condition component is a component that holds a boolean value.
/// Name serves as a unique identifier for the component.
/// Value is the actual boolean used by the component.
/// Planned to be used for user logic control, i.e if character is dead or rich
/// more functionality will be added later.
/// See <see cref="IComponent"/> for more info.
/// </summary>
public class ConditionComponent : IComponent
{
    public string Name { get; set; } = "New Condition";
    public bool Value { get; set; }
}
