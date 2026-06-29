using Tshin.Core.Models;

namespace Tshin.Core.Utils.Factories;

/// <summary>
/// Creates a new component of given type.
/// Type must implement <see cref="IComponent"/> otherwise it will throw.
/// </summary>
public static class ComponentFactory
{
    public static T Create<T>(string? id = null) where T : IComponent, new()
    {
        var component = new T();

        if (!string.IsNullOrWhiteSpace(id))
        {
            component.Name = id;
        }

        return component;
    }
}
