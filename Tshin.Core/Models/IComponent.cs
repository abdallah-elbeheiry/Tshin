namespace Tshin.Core.Models;

/// <summary>
/// IComponent is the base interface for all components.
/// Name must remain completely unique
/// </summary>
public interface IComponent
{
    string Name { get; set; }
}