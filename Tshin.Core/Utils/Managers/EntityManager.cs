using System;
using System.Collections.Generic;
using Tshin.Core.Models;

namespace Tshin.Core.Utils.Managers;

/// <summary>
/// Manages lifecycle and component storage for entities within the Entity Component System (ECS).
/// </summary>
public class EntityManager
{
    private readonly Dictionary<Entity, Dictionary<string, IComponent>> _entityComponents = new();

    /// <summary>
    /// Creates a new entity with an empty collection of components.
    /// </summary>
    /// <param name="id">An optional predefined <see cref="Guid"/> for the entity.</param>
    /// <returns>A unique <see cref="Entity"/> instance.</returns>
    public Entity CreateEntity(Guid? id = null)
    {
        var entity = id.HasValue ? new Entity { Id = id.Value } : new Entity();
        _entityComponents[entity] = new Dictionary<string, IComponent>();
        return entity;
    }

    /// <summary>
    /// Destroys the specified entity and removes all associated components from memory.
    /// </summary>
    /// <param name="entity">The entity to destroy.</param>
    public void DestroyEntity(Entity entity)
    {
        _entityComponents.Remove(entity);
    }

    /// <summary>
    /// Adds or updates a component assigned to a specific entity. 
    /// If a component with the same name already exists, it will be overwritten.
    /// </summary>
    /// <param name="entity">The entity to attach the component to.</param>
    /// <param name="component">The pure data component to add or update.</param>
    /// <exception cref="ArgumentException">Thrown when the specified entity does not exist in the manager.</exception>
    public void SetComponent(Entity entity, IComponent component)
    {
        if (!_entityComponents.TryGetValue(entity, out var components))
        {
            throw new ArgumentException("Entity does not exist.", nameof(entity));
        }

        components[component.Name] = component;
    }

    /// <summary>
    /// Retrieves a specific component attached to an entity, safely cast to its concrete type.
    /// </summary>
    /// <typeparam name="T">The expected concrete type of the component that implements <see cref="IComponent"/>.</typeparam>
    /// <param name="entity">The target entity.</param>
    /// <param name="componentName">The unique identifying name of the component.</param>
    /// <returns>The component cast to type <typeparamref name="T"/> if found and valid; otherwise, <see langword="null"/>.</returns>
    public T? GetComponent<T>(Entity entity, string componentName) where T : class, IComponent
    {
        if (_entityComponents.TryGetValue(entity, out var components) &&
            components.TryGetValue(componentName, out var component))
        {
            return component as T;
        }

        return null;
    }

    /// <summary>
    /// Retrieves all components currently assigned to a specific entity.
    /// </summary>
    /// <param name="entity">The target entity.</param>
    /// <returns>An enumerable collection of <see cref="IComponent"/> instances associated with the entity.</returns>
    public IEnumerable<IComponent> GetComponentsForEntity(Entity entity)
    {
        return _entityComponents.TryGetValue(entity, out var components)
            ? components.Values
            : [];
    }

    /// <summary>
    /// Removes a component from an entity by its name.
    /// </summary>
    /// <param name="entity">The target entity.</param>
    /// <param name="componentName">The unique identifying name of the component to remove.</param>
    /// <returns><see langword="true"/> if the component was successfully found and removed; otherwise, <see langword="false"/>.</returns>
    public bool RemoveComponent(Entity entity, string componentName)
    {
        return _entityComponents.TryGetValue(entity, out var components) && components.Remove(componentName);
    }

    /// <summary>
    /// Checks whether an entity currently has a specific component attached.
    /// </summary>
    /// <param name="entity">The target entity.</param>
    /// <param name="componentName">The name of the component to look for.</param>
    /// <returns><see langword="true"/> if the component exists on the entity; otherwise, <see langword="false"/>.</returns>
    public bool HasComponent(Entity entity, string componentName)
    {
        return _entityComponents.TryGetValue(entity, out var components) &&
               components.ContainsKey(componentName);
    }

    /// <summary>
    /// Retrieves a collection of all registered entities currently tracked by the manager.
    /// </summary>
    /// <returns>An enumerable collection of all active <see cref="Entity"/> IDs.</returns>
    public IEnumerable<Entity> GetAllEntities() => _entityComponents.Keys;

    /// <summary>
    /// Clears all tracked entities and their component collections from memory.
    /// </summary>
    public void ClearEntities() => _entityComponents.Clear();
}
