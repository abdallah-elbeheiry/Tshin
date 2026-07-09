using System;
using System.Linq;
using System.Text.RegularExpressions;
using Tshin.Core.Models;
using Tshin.Core.Utils.Managers;

namespace Tshin.Utilities;

public static class VariableInterpolator
{
    private static readonly Regex UuidPattern = new(
        @"\{([0-9a-fA-F\-]{36})\.([a-zA-Z0-9_]+)\}",
        RegexOptions.Compiled);

    /// <summary>
    /// Matches {EntityName.ComponentName} where the entity name can contain letters,
    /// digits, spaces and underscores.
    /// </summary>
    private static readonly Regex NamePattern = new(
        @"\{([a-zA-Z0-9_ ]+)\.([a-zA-Z0-9_]+)\}",
        RegexOptions.Compiled);

    public static string Interpolate(string? text, EntityManager entityManager)
    {
        if (string.IsNullOrEmpty(text) || entityManager is null)
            return text ?? string.Empty;

        // Resolve {UUID.ComponentName} first (precise)
        var result = UuidPattern.Replace(text, match => Resolve(match.Groups[1].Value,
            match.Groups[2].Value, entityManager, match.Value));

        // Then resolve {EntityName.ComponentName}
        result = NamePattern.Replace(result, match => ResolveByName(match.Groups[1].Value,
            match.Groups[2].Value, entityManager, match.Value));

        return result;
    }

    private static string Resolve(string entityId, string componentName,
        EntityManager entityManager, string fallback)
    {
        if (!Guid.TryParse(entityId, out var guid))
            return fallback;

        var entity = entityManager.FindEntity(guid);
        if (entity is null)
            return fallback;

        return ResolveComponent(entity, componentName, entityManager, fallback);
    }

    private static string ResolveByName(string entityName, string componentName,
        EntityManager entityManager, string fallback)
    {
        var entity = entityManager.GetAllEntities()
            .FirstOrDefault(e => e.Name.Equals(entityName, StringComparison.OrdinalIgnoreCase));

        if (entity is null)
            return fallback;

        return ResolveComponent(entity, componentName, entityManager, fallback);
    }

    private static string ResolveComponent(Entity entity, string componentName,
        EntityManager entityManager, string fallback)
    {
        var component = entityManager.GetComponent<IComponent>(entity, componentName);
        if (component is null)
            return fallback;

        return component switch
        {
            NumberComponent n => n.Value.ToString("0.##"),
            TextComponent t => t.Value,
            ConditionComponent c => c.Value ? "true" : "false",
            _ => fallback
        };
    }
}
