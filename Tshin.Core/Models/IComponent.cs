namespace Tshin.Core.Models;

/// <summary>
/// IComponent is the base interface for all components.
/// Equals compares by Value of the components that implement this interface, not by reference.
/// Id must remain completely unique
/// </summary>
public interface IComponent : IEquatable<IComponent>
{
    string Id { get; set; }
    ComponentType Type { get; }
}