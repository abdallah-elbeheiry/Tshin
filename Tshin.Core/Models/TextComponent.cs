namespace Tshin.Core.Models;


/// <summary>
/// A text component is a component that holds a string.
/// Id serves as a unique identifier for the component.
/// Value is the actual string used by the component.
/// see <see cref="IComponent"/> for more info.
/// </summary>
public class TextComponent : IComponent
{
    public string Id { get; set; } = "New Text";
    public ComponentType Type => ComponentType.Text;
    public string Value { get; set; } = string.Empty;

    public bool Equals(IComponent? other)
    {
        return other switch
        {
            TextComponent t => Value.Equals(t.Value),
            _ => false
        };
    }
}