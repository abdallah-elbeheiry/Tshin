namespace Tshin.Core.Models;


/// <summary>
/// A text component is a component that holds a string.
/// Name serves as a unique identifier for the component.
/// Value is the actual string used by the component.
/// see <see cref="IComponent"/> for more info.
/// </summary>
public class TextComponent : IComponent
{
    public string Name { get; set; } = "New Text";
    public string Value { get; set; } = string.Empty;
}
