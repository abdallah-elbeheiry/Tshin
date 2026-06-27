namespace Tshin.Core.Models;


/// <summary>
/// A unique identifier for an entity.
/// an entity can be a player, or an enemy
/// an entity (and its components) are pure data, check <see cref="Utils.Managers.EntityManager"/> for implementation details.
/// 1 entity can have multiple components, and a component can be attached to multiple entities
/// </summary>
public struct Entity() : IEquatable<Entity>
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public bool Equals(Entity other) => Id.Equals(other.Id);
    public override bool Equals(object? obj) => obj is Entity other && Equals(other);
    public override int GetHashCode() => Id.GetHashCode();
    public static bool operator ==(Entity left, Entity right) => left.Equals(right);
    public static bool operator !=(Entity left, Entity right) => !left.Equals(right);
}